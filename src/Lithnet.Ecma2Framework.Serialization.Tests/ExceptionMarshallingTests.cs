using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    /// <summary>
    /// Exercises the Path C exception-marshalling round-trip (mapping Section D): a worker-thrown exception is
    /// captured into an <see cref="MmsExceptionEnvelope"/>, serialised to the JSON-RPC error-data string, then
    /// reconstructed on the shim side into the EXACT real host exception (or the non-host carrier). The
    /// completeness guard asserts every one of the 42 host exception types and every declared field is mapped.
    /// </summary>
    [TestClass]
    public class ExceptionMarshallingTests
    {
        // A full worker -> wire -> shim round-trip: build the envelope, serialise to the error-data string,
        // deserialise, and reconstruct.
        private static Exception RoundTrip(Exception thrown)
        {
            MmsExceptionEnvelope envelope = MmsExceptionEnvelopeFactory.FromException(thrown);
            string json = MmsExceptionEnvelopeSerializer.Serialize(envelope);
            MmsExceptionEnvelope rehydrated = MmsExceptionEnvelopeSerializer.Deserialize(json);
            return MmsExceptionReconstructor.Reconstruct(rehydrated);
        }

        // -------------------------------------------------------------------------
        // Completeness guard (no curation): every host exception type + every declared field is mapped.
        // -------------------------------------------------------------------------

        [TestMethod]
        public void HostHierarchyHasExactlyFortyTwoExceptionTypes()
        {
            int count = ExceptionCompletenessGuard.EnumerateHostExceptionTypes().Count;
            Assert.AreEqual(42, count, "The host exception hierarchy must enumerate exactly 42 types (mapping Section D).");
        }

        [TestMethod]
        public void EveryHostExceptionTypeIsMappedByTheRegistry()
        {
            List<string> unmapped = ExceptionCompletenessGuard.UnmappedTypes();

            if (unmapped.Count == 0)
            {
                return;
            }

            Assert.Fail(
                "The reconstruction registry does not cover these host exception types (add them to " +
                "MmsExceptionTypeRegistry): " + string.Join(", ", unmapped));
        }

        [TestMethod]
        public void EveryDeclaredFieldOnEveryHostExceptionTypeIsCarried()
        {
            SortedDictionary<string, List<string>> uncarried = ExceptionCompletenessGuard.UncarriedDeclaredFields();

            if (uncarried.Count == 0)
            {
                return;
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine("These declared backing fields are not carried by the exception envelope registry");
            message.AppendLine("(add them to MmsExceptionTypeRegistry.DeclaredFieldsByType + the envelope):");
            foreach (KeyValuePair<string, List<string>> entry in uncarried)
            {
                message.AppendLine($"  {entry.Key}: {string.Join(", ", entry.Value)}");
            }

            Assert.Fail(message.ToString());
        }

        [TestMethod]
        public void EveryHostExceptionTypeReconstructsToItsExactType()
        {
            // The ultimate guard: build a minimal instance of every host exception type, round-trip it, and
            // assert the reconstructed object is the EXACT same runtime type with the carried message.
            foreach (Type type in ExceptionCompletenessGuard.EnumerateHostExceptionTypes())
            {
                Exception original = (Exception)Activator.CreateInstance(type, true);
                SetMessage(original, "msg-" + type.Name);

                Exception rebuilt = RoundTrip(original);

                Assert.IsNotNull(rebuilt, $"{type.Name} reconstructed to null.");
                Assert.AreEqual(type, rebuilt.GetType(), $"{type.Name} did not reconstruct to its exact type.");
                Assert.AreEqual("msg-" + type.Name, rebuilt.Message, $"{type.Name} message not preserved.");
            }
        }

        // -------------------------------------------------------------------------
        // Representative round-trips
        // -------------------------------------------------------------------------

        [TestMethod]
        public void TreeADnTypePreservesMaNameAndDn()
        {
            NoSuchObjectException original = new NoSuchObjectException("contoso-ma", "CN=jdoe,OU=Users,DC=contoso,DC=com");

            Exception rebuilt = RoundTrip(original);

            Assert.IsInstanceOfType(rebuilt, typeof(NoSuchObjectException), "Exact host type must be reconstructed.");
            NoSuchObjectException typed = (NoSuchObjectException)rebuilt;
            Assert.AreEqual("contoso-ma", typed.MAName, "MAName (m_MAName) must survive.");
            // DN : ReferenceValue getter recomputes on-engine via Utils.MAs[MAName].CreateDN(m_DN); off-engine it
            // is not resolvable, so we assert the carried m_DN backing field directly via reflection.
            string dn = (string)typeof(NoSuchObjectException)
                .GetField("m_DN", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(typed);
            Assert.AreEqual("CN=jdoe,OU=Users,DC=contoso,DC=com", dn, "DN (m_DN) must survive.");
        }

        [TestMethod]
        public void NoCompatiblePartitionFoundPreservesAllFourFields()
        {
            string[] classes = { "user", "person", "top" };
            NoCompatiblePartitionFoundException original =
                new NoCompatiblePartitionFoundException("contoso-ma", "CN=x,DC=contoso,DC=com", classes, "user");

            Exception rebuilt = RoundTrip(original);

            Assert.IsInstanceOfType(rebuilt, typeof(NoCompatiblePartitionFoundException));
            NoCompatiblePartitionFoundException typed = (NoCompatiblePartitionFoundException)rebuilt;
            Assert.AreEqual("contoso-ma", typed.MAName, "MAName must survive.");
            CollectionAssert.AreEqual(classes, (System.Collections.ICollection)typed.ObjectClasses, "ObjectClasses[] must survive.");
            Assert.AreEqual("user", typed.ObjectType, "ObjectType (m_PrimaryObjectClass) must survive.");
        }

        [TestMethod]
        public void TreeAFieldTypePreservesAttributeName()
        {
            NoSuchAttributeException original = new NoSuchAttributeException("employeeNumber");

            Exception rebuilt = RoundTrip(original);

            Assert.IsInstanceOfType(rebuilt, typeof(NoSuchAttributeException), "Exact host type must be reconstructed.");
            Assert.AreEqual("employeeNumber", ((NoSuchAttributeException)rebuilt).AttributeName, "AttributeName must survive.");
        }

        [TestMethod]
        public void NoSuchParameterPreservesParameterName()
        {
            NoSuchParameterException original = new NoSuchParameterException("Server");

            Exception rebuilt = RoundTrip(original);

            Assert.IsInstanceOfType(rebuilt, typeof(NoSuchParameterException));
            Assert.AreEqual("Server", ((NoSuchParameterException)rebuilt).ParameterName, "ParameterName must survive.");
        }

        [TestMethod]
        public void NoSuchObjectTypePreservesObjectTypeBackedByClassNameField()
        {
            // Getter is ObjectType, backing field is m_className (mapping Section D nuance).
            NoSuchObjectTypeException original = new NoSuchObjectTypeException("groupOfNames");

            Exception rebuilt = RoundTrip(original);

            Assert.IsInstanceOfType(rebuilt, typeof(NoSuchObjectTypeException));
            Assert.AreEqual("groupOfNames", ((NoSuchObjectTypeException)rebuilt).ObjectType, "ObjectType (m_className) must survive.");
        }

        [TestMethod]
        public void TreeBTypeReconstructsExactType()
        {
            // WarningNoWatermarkException is treated as a WARNING by FIM; the exact type must be thrown, not a
            // generic surrogate, or FIM mishandles it.
            WarningNoWatermarkException original = new WarningNoWatermarkException("no watermark this run");

            Exception rebuilt = RoundTrip(original);

            Assert.AreEqual(typeof(WarningNoWatermarkException), rebuilt.GetType(), "EXACT Tree-B type must be reconstructed.");
            Assert.AreEqual("no watermark this run", rebuilt.Message, "Message must survive.");
        }

        [TestMethod]
        public void DistinctTreeBPasswordTypesStayDistinct()
        {
            Exception incorrect = RoundTrip(new OldPasswordIncorrectException("old pw wrong"));
            Exception policy = RoundTrip(new PasswordPolicyViolationException("too short"));

            Assert.AreEqual(typeof(OldPasswordIncorrectException), incorrect.GetType());
            Assert.AreEqual(typeof(PasswordPolicyViolationException), policy.GetType());
            Assert.AreNotEqual(incorrect.GetType(), policy.GetType(), "The two password failure types must stay distinct.");
        }

        [TestMethod]
        public void TwoDeepInnerChainIsPreserved()
        {
            // host (outer) -> host (middle) -> non-host (innermost)
            Exception innermost = new InvalidOperationException("root cause");
            NoSuchAttributeException middle = new NoSuchAttributeException("attr1");
            SetInner(middle, innermost);
            InvalidDNException outer = new InvalidDNException("ma", "CN=bad");
            SetInner(outer, middle);

            Exception rebuilt = RoundTrip(outer);

            Assert.IsInstanceOfType(rebuilt, typeof(InvalidDNException), "Outer host type must survive.");
            Assert.AreEqual("ma", ((InvalidDNException)rebuilt).MAName, "Outer MAName must survive.");

            Assert.IsNotNull(rebuilt.InnerException, "Middle link must survive.");
            Assert.IsInstanceOfType(rebuilt.InnerException, typeof(NoSuchAttributeException), "Middle host type must survive.");
            Assert.AreEqual("attr1", ((NoSuchAttributeException)rebuilt.InnerException).AttributeName, "Middle AttributeName must survive.");

            Assert.IsNotNull(rebuilt.InnerException.InnerException, "Innermost link must survive.");
            // The innermost is non-host -> carrier (ExtensibleExtensionException) preserving the type name + msg.
            Assert.IsInstanceOfType(rebuilt.InnerException.InnerException, typeof(ExtensibleExtensionException), "Innermost non-host link surfaces as the carrier.");
            Assert.IsTrue(rebuilt.InnerException.InnerException.Message.Contains("System.InvalidOperationException"), "Innermost concrete type name preserved.");
            Assert.IsTrue(rebuilt.InnerException.InnerException.Message.Contains("root cause"), "Innermost message preserved.");
        }

        [TestMethod]
        public void NonHostCarrierPreservesTypeNameMessageAndInner()
        {
            Exception inner = new TimeoutException("inner timed out");
            Exception thrown = new InvalidOperationException("provider blew up", inner);

            Exception rebuilt = RoundTrip(thrown);

            // A non-host worker exception surfaces as a host ExtensibleExtensionException carrier so FIM still
            // sees a host extension exception, with the concrete type + message preserved and the inner rebuilt.
            Assert.IsInstanceOfType(rebuilt, typeof(ExtensibleExtensionException), "Non-host exception surfaces as the host carrier.");
            Assert.IsTrue(rebuilt.Message.Contains("System.InvalidOperationException"), "Concrete worker type name preserved.");
            Assert.IsTrue(rebuilt.Message.Contains("provider blew up"), "Original message preserved.");

            Assert.IsNotNull(rebuilt.InnerException, "Inner chain preserved.");
            Assert.IsInstanceOfType(rebuilt.InnerException, typeof(ExtensibleExtensionException), "Inner non-host link is also a carrier.");
            Assert.IsTrue(rebuilt.InnerException.Message.Contains("System.TimeoutException"), "Inner type name preserved.");
            Assert.IsTrue(rebuilt.InnerException.Message.Contains("inner timed out"), "Inner message preserved.");
        }

        [TestMethod]
        public void ReconstructorThrowsExactHostType()
        {
            // The Throw path (used by the shim) must throw the exact host type so catch(WarningNoWatermarkException)
            // fires.
            MmsExceptionEnvelope envelope =
                MmsExceptionEnvelopeSerializer.Deserialize(
                    MmsExceptionEnvelopeSerializer.Serialize(
                        MmsExceptionEnvelopeFactory.FromException(new ServerDownException("down"))));

            Assert.Throws<ServerDownException>(() => MmsExceptionReconstructor.Throw(envelope));
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static void SetMessage(Exception ex, string message)
        {
            typeof(Exception)
                .GetField("_message", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(ex, message);
        }

        private static void SetInner(Exception ex, Exception inner)
        {
            typeof(Exception)
                .GetField("_innerException", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(ex, inner);
        }
    }
}
