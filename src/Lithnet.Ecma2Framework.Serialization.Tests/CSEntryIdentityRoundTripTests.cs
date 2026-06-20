using System.Collections.Generic;
using System.Linq;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    /// <summary>
    /// Round-trip coverage for the framework-owned <see cref="CSEntryIdentity"/> password-path carrier.
    /// Unlike the host MMS types, this is our own plain data object, so the completeness guard (which keys
    /// on the <c>Microsoft.MetadirectoryServices</c> namespace) does not apply; these tests instead assert
    /// that every field — including the anchor attribute value(s), the GAP-7 fix — survives the
    /// MmsPipeSerializer round-trip.
    /// </summary>
    [TestClass]
    public class CSEntryIdentityRoundTripTests
    {
        [TestMethod]
        public void CSEntryIdentityRoundTripsAllIdentityFields()
        {
            CSEntryIdentity identity = new CSEntryIdentity
            {
                DN = "CN=alice,OU=People,DC=example,DC=com",
                RDN = "CN=alice",
                ObjectType = "user",
                MAName = "Example Okta MA",
            };
            identity.ObjectClass.Add("top");
            identity.ObjectClass.Add("person");
            identity.ObjectClass.Add("user");

            string xml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(identity);
            CSEntryIdentity rt = MmsPipeSerializer.DeserializeXml<CSEntryIdentity>(xml);

            Assert.AreEqual("CN=alice,OU=People,DC=example,DC=com", rt.DN, "DN");
            Assert.AreEqual("CN=alice", rt.RDN, "RDN");
            Assert.AreEqual("user", rt.ObjectType, "ObjectType");
            Assert.AreEqual("Example Okta MA", rt.MAName, "MAName");
            CollectionAssert.AreEqual(
                new[] { "top", "person", "user" },
                rt.ObjectClass.ToArray(),
                "ObjectClass");
        }

        [TestMethod]
        public void CSEntryIdentityRoundTripsAnchorAttributeWithValue()
        {
            // The GAP-7 fix: a directory that keys on an anchor rather than the DN must be able to find the
            // account. The anchor attribute and its value must survive the round-trip.
            CSEntryIdentity identity = new CSEntryIdentity
            {
                DN = "CN=bob",
                ObjectType = "user",
            };
            identity.AddAttribute(new CSEntryIdentityAttribute(
                "oktaId",
                AttributeType.String,
                false,
                new List<object> { "00u1abcdEFGH" }));

            string xml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(identity);
            CSEntryIdentity rt = MmsPipeSerializer.DeserializeXml<CSEntryIdentity>(xml);

            CSEntryIdentityAttribute anchor = rt.GetAttribute("oktaId");
            Assert.IsNotNull(anchor, "the anchor attribute must survive the round-trip");
            Assert.AreEqual("oktaId", anchor.Name, "anchor Name");
            Assert.AreEqual(AttributeType.String, anchor.DataType, "anchor DataType");
            Assert.IsFalse(anchor.IsMultivalued, "anchor IsMultivalued");
            Assert.AreEqual("00u1abcdEFGH", anchor.Value, "anchor value (GAP-7: the account locator must survive)");

            // Accessors resolve by name (case-insensitive) and return the value.
            Assert.AreEqual("00u1abcdEFGH", rt.GetValue("OKTAID"), "GetValue is case-insensitive");
            Assert.AreEqual("00u1abcdEFGH", rt["oktaId"].Value, "indexer accessor");
        }

        [TestMethod]
        public void CSEntryIdentityRoundTripsTypedAndMultiValuedAttributes()
        {
            CSEntryIdentity identity = new CSEntryIdentity
            {
                DN = "CN=carol",
                ObjectType = "user",
            };

            // String single
            identity.AddAttribute(new CSEntryIdentityAttribute(
                "displayName", AttributeType.String, false, new List<object> { "Carol Smith" }));

            // Integer single — DataContractSerializer preserves the boxed long with no typed-value envelope.
            identity.AddAttribute(new CSEntryIdentityAttribute(
                "employeeNumber", AttributeType.Integer, false, new List<object> { 4815162342L }));

            // Boolean single
            identity.AddAttribute(new CSEntryIdentityAttribute(
                "accountEnabled", AttributeType.Boolean, false, new List<object> { true }));

            // Binary single
            byte[] objectGuid = new byte[] { 0x01, 0x02, 0x03, 0x04, 0xFF };
            identity.AddAttribute(new CSEntryIdentityAttribute(
                "objectGuid", AttributeType.Binary, false, new List<object> { objectGuid }));

            // String multi
            identity.AddAttribute(new CSEntryIdentityAttribute(
                "proxyAddresses", AttributeType.String, true, new List<object> { "smtp:carol@example.com", "smtp:c.smith@example.com" }));

            string xml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(identity);
            CSEntryIdentity rt = MmsPipeSerializer.DeserializeXml<CSEntryIdentity>(xml);

            Assert.AreEqual("Carol Smith", rt.GetValue("displayName"), "string single value");

            object employeeNumber = rt.GetValue("employeeNumber");
            Assert.IsInstanceOfType(employeeNumber, typeof(long), "integer value preserved as boxed long");
            Assert.AreEqual(4815162342L, (long)employeeNumber, "integer single value");

            object accountEnabled = rt.GetValue("accountEnabled");
            Assert.IsInstanceOfType(accountEnabled, typeof(bool), "boolean value preserved as boxed bool");
            Assert.IsTrue((bool)accountEnabled, "boolean single value");

            object roundTrippedGuid = rt.GetValue("objectGuid");
            Assert.IsInstanceOfType(roundTrippedGuid, typeof(byte[]), "binary value preserved as byte[]");
            CollectionAssert.AreEqual(objectGuid, (byte[])roundTrippedGuid, "binary single value");

            CSEntryIdentityAttribute proxyAddresses = rt.GetAttribute("proxyAddresses");
            Assert.IsNotNull(proxyAddresses, "multi-valued attribute survives");
            Assert.IsTrue(proxyAddresses.IsMultivalued, "IsMultivalued survives");
            CollectionAssert.AreEqual(
                new object[] { "smtp:carol@example.com", "smtp:c.smith@example.com" },
                proxyAddresses.Values.ToArray(),
                "multi-valued string values survive in order");
        }

        [TestMethod]
        public void CSEntryIdentityRoundTripsWithNoAttributes()
        {
            // A minimal identity (DN-keyed directory, no carried attributes) must still round-trip cleanly.
            CSEntryIdentity identity = new CSEntryIdentity
            {
                DN = "CN=dave",
                RDN = "CN=dave",
                ObjectType = "user",
            };

            string xml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(identity);
            CSEntryIdentity rt = MmsPipeSerializer.DeserializeXml<CSEntryIdentity>(xml);

            Assert.AreEqual("CN=dave", rt.DN, "DN");
            Assert.AreEqual(0, rt.Attributes.Count, "no attributes");
            Assert.IsNull(rt.GetValue("missing"), "GetValue returns null for an absent attribute");
            Assert.IsNull(rt["missing"], "indexer returns null for an absent attribute");
        }
    }
}
