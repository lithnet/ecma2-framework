using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MetadirectoryServices;
using Microsoft.MetadirectoryServices.DetachedObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    [TestClass]
    public class MmsPipeSerializerTests
    {
        /// <summary>
        /// On EXPORT the host builds the CSEntryChange and populates each AttributeChange.DataType from the
        /// schema. (Provider-built import changes, made via AttributeChange.CreateAttributeAdd, are
        /// DataType=Undefined by contrast — that factory cannot set a type.) The export merge,
        /// AttributeChangeExtensions.ApplyAttributeChanges, switches on that DataType to choose the value
        /// comparer, so a populated DataType MUST survive the transport intact. This test builds the changes
        /// the way the host does — AttributeChangeDetached with an explicit DataType — and asserts the type,
        /// the IsMultiValued flag, and the value-change set all round-trip through the pipe serializer for
        /// every AttributeType the merge handles.
        /// </summary>
        [TestMethod]
        public void CSEntryChangeRoundTripsHostPopulatedAttributeChangeDataType()
        {
            // Object=Update is the export shape that carries Replace/Update/Add/Delete attribute changes
            // (the host's DetachedUtils.AttributeModificationRules permits exactly that set under Update).
            CSEntryChange cse = CSEntryChange.Create();
            cse.ObjectModificationType = ObjectModificationType.Update;
            cse.DN = "CN=jdoe";
            cse.ObjectType = "person";

            cse.AttributeChanges.Add(BuildPopulatedChange(
                "displayName", AttributeType.String, false,
                new ValueChange("John Doe", ValueModificationType.Add)));

            cse.AttributeChanges.Add(BuildPopulatedChange(
                "employeeNumber", AttributeType.Integer, false,
                new ValueChange(9999999999L, ValueModificationType.Add)));

            cse.AttributeChanges.Add(BuildPopulatedChange(
                "photo", AttributeType.Binary, false,
                new ValueChange(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, ValueModificationType.Add)));

            cse.AttributeChanges.Add(BuildPopulatedChange(
                "manager", AttributeType.Reference, false,
                new ValueChange("CN=boss", ValueModificationType.Add)));

            cse.AttributeChanges.Add(BuildPopulatedChange(
                "suspended", AttributeType.Boolean, false,
                new ValueChange(true, ValueModificationType.Add)));

            // A multivalued reference UPDATE carrying both an add and a delete — the shape the export merge
            // resolves for MV attributes. Both value changes (and the MV flag) must survive.
            cse.AttributeChanges.Add(new AttributeChangeDetached(
                "memberOf",
                AttributeModificationType.Update,
                new List<ValueChange>
                {
                    new ValueChange("CN=g1,OU=groups", ValueModificationType.Add),
                    new ValueChange("CN=g2,OU=groups", ValueModificationType.Delete),
                },
                AttributeType.Reference,
                true,
                false));

            string xml = MmsPipeSerializer.SerializeXml<CSEntryChange>(cse);
            CSEntryChange rt = MmsPipeSerializer.DeserializeXml<CSEntryChange>(xml);

            Assert.AreEqual(6, rt.AttributeChanges.Count, "AttributeChanges count");

            AssertChangeShape(rt, "displayName", AttributeType.String, false);
            AssertChangeShape(rt, "employeeNumber", AttributeType.Integer, false);
            AssertChangeShape(rt, "photo", AttributeType.Binary, false);
            AssertChangeShape(rt, "manager", AttributeType.Reference, false);
            AssertChangeShape(rt, "suspended", AttributeType.Boolean, false);
            AssertChangeShape(rt, "memberOf", AttributeType.Reference, true);

            // The MV reference update preserves the full value-change set (add + delete), not just the adds.
            AttributeChange memberOf = rt.AttributeChanges["memberOf"];
            Assert.AreEqual(AttributeModificationType.Update, memberOf.ModificationType, "memberOf ModificationType");
            Assert.AreEqual(2, memberOf.ValueChanges.Count, "memberOf value-change count");
            Assert.AreEqual(1, memberOf.ValueChanges.Count(v => v.ModificationType == ValueModificationType.Add), "memberOf adds");
            Assert.AreEqual(1, memberOf.ValueChanges.Count(v => v.ModificationType == ValueModificationType.Delete), "memberOf deletes");
        }

        private static AttributeChangeDetached BuildPopulatedChange(string name, AttributeType dataType, bool isMultiValued, ValueChange valueChange)
        {
            return new AttributeChangeDetached(
                name,
                AttributeModificationType.Replace,
                new List<ValueChange> { valueChange },
                dataType,
                isMultiValued,
                false);
        }

        private static void AssertChangeShape(CSEntryChange rt, string name, AttributeType expectedDataType, bool expectedMultiValued)
        {
            Assert.IsTrue(rt.AttributeChanges.Contains(name), name + " present");
            AttributeChange change = rt.AttributeChanges[name];
            Assert.AreEqual(expectedDataType, change.DataType, name + " DataType must survive the round-trip");
            Assert.AreEqual(expectedMultiValued, change.IsMultiValued, name + " IsMultiValued must survive the round-trip");
        }

        [TestMethod]
        public void CSEntryChangeRoundTripsWithErrorFieldsAndInt64()
        {
            // Build a REAL CSEntryChange carrying the fields the old mirror dropped (ErrorName/
            // ErrorDetail), a String value, an Int64 value > Int32.MaxValue, and an AnchorAttribute.
            CSEntryChange cse = CSEntryChange.Create();
            cse.ObjectModificationType = ObjectModificationType.Add;
            cse.DN = "CN=jdoe";
            cse.ObjectType = "person";
            cse.ErrorName = "anImportError";
            cse.ErrorDetail = "the gory details";
            cse.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("displayName", "John Doe"));
            cse.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("employeeNumber", 9999999999L));
            cse.AnchorAttributes.Add(AnchorAttribute.Create("id", "abc-123"));

            Guid originalId = cse.Identifier;

            string xml = MmsPipeSerializer.SerializeXml<CSEntryChange>(cse);
            CSEntryChange rt = MmsPipeSerializer.DeserializeXml<CSEntryChange>(xml);

            Assert.AreEqual(originalId, rt.Identifier, "Identifier");
            Assert.AreEqual(ObjectModificationType.Add, rt.ObjectModificationType, "ObjectModificationType");
            Assert.AreEqual("CN=jdoe", rt.DN, "DN");
            Assert.AreEqual("person", rt.ObjectType, "ObjectType");
            Assert.AreEqual("anImportError", rt.ErrorName, "ErrorName (mirror dropped this)");
            Assert.AreEqual("the gory details", rt.ErrorDetail, "ErrorDetail (mirror dropped this)");
            Assert.AreEqual(2, rt.AttributeChanges.Count, "AttributeChanges count");
            Assert.AreEqual(1, rt.AnchorAttributes.Count, "AnchorAttributes count");
            Assert.AreEqual("abc-123", (string)rt.AnchorAttributes[0].Value, "Anchor value");

            Assert.IsTrue(rt.AttributeChanges.Contains("displayName"), "displayName present");
            string displayName = rt.AttributeChanges["displayName"].ValueChanges.First().Value as string;
            Assert.AreEqual("John Doe", displayName, "String value preserved");

            Assert.IsTrue(rt.AttributeChanges.Contains("employeeNumber"), "employeeNumber present");
            object empNum = rt.AttributeChanges["employeeNumber"].ValueChanges.First().Value;
            Assert.IsInstanceOfType(empNum, typeof(long), "Int64 returns as a long (no typed-value envelope)");
            Assert.AreEqual(9999999999L, (long)empNum, "Int64 value preserved");
        }

        [TestMethod]
        public void SchemaRoundTrips()
        {
            Schema schema = Schema.Create();

            SchemaType person = SchemaType.Create("person", false);
            person.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String, AttributeOperation.ImportOnly));
            person.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String, AttributeOperation.ImportExport));
            person.Attributes.Add(SchemaAttribute.CreateMultiValuedAttribute("memberOf", AttributeType.Reference, AttributeOperation.ImportExport));
            schema.Types.Add(person);

            string xml = MmsPipeSerializer.SerializeXml<Schema>(schema);
            Schema rt = MmsPipeSerializer.DeserializeXml<Schema>(xml);

            Assert.AreEqual(1, rt.Types.Count, "Types count");
            SchemaType rtPerson = rt.Types.First();
            Assert.AreEqual("person", rtPerson.Name, "SchemaType name");
            Assert.AreEqual(3, rtPerson.Attributes.Count, "Attributes count");

            SchemaAttribute idAttr = rtPerson.Attributes.First(a => a.Name == "id");
            Assert.IsTrue(idAttr.IsAnchor, "id is anchor");
            Assert.AreEqual(AttributeType.String, idAttr.DataType, "id data type");
            Assert.AreEqual(AttributeOperation.ImportOnly, idAttr.AllowedAttributeOperation, "id operation");

            SchemaAttribute memberOf = rtPerson.Attributes.First(a => a.Name == "memberOf");
            Assert.IsTrue(memberOf.IsMultiValued, "memberOf is multivalued");
            Assert.AreEqual(AttributeType.Reference, memberOf.DataType, "memberOf data type");
        }

        [TestMethod]
        public void CSEntryChangeResultRoundTrips()
        {
            Guid id = Guid.NewGuid();
            CSEntryChangeResult result = CSEntryChangeResult.Create(id, null, MAExportError.ExportErrorConnectedDirectoryError, "permErr", "could not write");

            string xml = MmsPipeSerializer.SerializeXml<CSEntryChangeResult>(result);
            CSEntryChangeResult rt = MmsPipeSerializer.DeserializeXml<CSEntryChangeResult>(xml);

            Assert.AreEqual(id, rt.Identifier, "Identifier");
            Assert.AreEqual(MAExportError.ExportErrorConnectedDirectoryError, rt.ErrorCode, "ErrorCode");
            Assert.AreEqual("permErr", rt.ErrorName, "ErrorName");
            Assert.AreEqual("could not write", rt.ErrorDetail, "ErrorDetail");
        }
    }
}
