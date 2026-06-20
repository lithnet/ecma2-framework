using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MetadirectoryServices;
using Microsoft.MetadirectoryServices.DetachedObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Tests
{
    /// <summary>
    /// Tests for the MMS helper APIs ported into the framework. These drive the helpers against real
    /// <see cref="Microsoft.MetadirectoryServices"/> types built via the host factory methods.
    /// </summary>
    [TestClass]
    public class MmsHelperTests
    {
        // ---------------------------------------------------------------------
        // GetValueAdd / GetValueAdds / GetValueDeletes
        // ---------------------------------------------------------------------

        [TestMethod]
        public void GetValueAdd_ReturnsSingleAddedString()
        {
            AttributeChange change = AttributeChange.CreateAttributeAdd("displayName", "hello");

            string value = change.GetValueAdd<string>();

            Assert.AreEqual("hello", value);
        }

        [TestMethod]
        public void GetValueAdd_ReturnsDefaultWhenNoAdds()
        {
            AttributeChange change = AttributeChange.CreateAttributeDelete("displayName");

            string value = change.GetValueAdd<string>();

            Assert.IsNull(value);
        }

        [TestMethod]
        public void GetValueAdds_ReturnsAllAddedValues()
        {
            List<object> values = new List<object> { "a", "b", "c" };
            AttributeChange change = AttributeChange.CreateAttributeAdd("member", values);

            IList<string> adds = change.GetValueAdds<string>();

            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, adds.ToArray());
        }

        [TestMethod]
        public void GetValueAdds_ReturnsLongValues()
        {
            List<object> values = new List<object> { 1L, 2L, 3L };
            AttributeChange change = AttributeChange.CreateAttributeAdd("counts", values);

            IList<long> adds = change.GetValueAdds<long>();

            CollectionAssert.AreEquivalent(new[] { 1L, 2L, 3L }, adds.ToArray());
        }

        [TestMethod]
        public void GetValueDeletes_ReturnsDeletedValuesOnUpdate()
        {
            List<ValueChange> valueChanges = new List<ValueChange>
            {
                ValueChange.CreateValueAdd("addme"),
                ValueChange.CreateValueDelete("removeme"),
            };

            AttributeChange change = AttributeChange.CreateAttributeUpdate("member", valueChanges);

            IList<string> deletes = change.GetValueDeletes<string>();
            IList<string> adds = change.GetValueAdds<string>();

            CollectionAssert.AreEquivalent(new[] { "removeme" }, deletes.ToArray());
            CollectionAssert.AreEquivalent(new[] { "addme" }, adds.ToArray());
        }

        [TestMethod]
        public void GetValueAdds_EmptyWhenNoValueChanges()
        {
            AttributeChange change = AttributeChange.CreateAttributeDelete("member");

            IList<string> adds = change.GetValueAdds<string>();

            Assert.AreEqual(0, adds.Count);
        }

        // ---------------------------------------------------------------------
        // ApplyAttributeChanges — the export-merge entry point the provider calls:
        // change.ApplyAttributeChanges(existing), which switches on change.DataType.
        // On EXPORT the host populates DataType from the schema (modelled here with
        // AttributeChangeDetached); provider-built import changes (the host factories)
        // are DataType=Undefined and are never fed to this merge.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Builds an export-shaped <see cref="AttributeChange"/> with an explicit <paramref name="dataType"/>,
        /// the way the host populates the changes it hands an export provider. The host factory methods
        /// (CreateAttributeAdd etc.) cannot set a DataType — they always yield Undefined — so they are not
        /// usable for exercising the DataType-driven merge.
        /// </summary>
        private static AttributeChange BuildExportChange(string name, AttributeModificationType modificationType, AttributeType dataType, bool isMultiValued, params ValueChange[] valueChanges)
        {
            return new AttributeChangeDetached(name, modificationType, new List<ValueChange>(valueChanges), dataType, isMultiValued, false);
        }

        [TestMethod]
        public void ApplyAttributeChanges_FactoryBuiltChange_HasUndefinedDataType()
        {
            // Documents the host contract: changes built by the factory methods (the import direction) carry
            // an Undefined DataType. The export-merge entry point therefore relies on the host populating
            // DataType on the changes it delivers to an export provider.
            AttributeChange change = AttributeChange.CreateAttributeAdd("member", new List<object> { "a" });

            Assert.AreEqual(AttributeType.Undefined, change.DataType);
        }

        [TestMethod]
        public void ApplyAttributeChanges_ThrowsForUndefinedDataTypeChange()
        {
            // The merge reads change.DataType; for an Undefined-typed (factory-built) change it fails loudly
            // rather than silently mis-merging the set.
            Assert.Throws<UnknownOrUnsupportedDataTypeException>(() =>
            {
                AttributeChange change = AttributeChange.CreateAttributeAdd("member", new List<object> { "a" });

                change.ApplyAttributeChanges(new List<object>());
            });
        }

        [TestMethod]
        public void ApplyAttributeChanges_StringMerge()
        {
            List<object> existing = new List<object> { "a", "b" };
            AttributeChange change = BuildExportChange("member", AttributeModificationType.Update, AttributeType.String, true,
                ValueChange.CreateValueAdd("c"),
                ValueChange.CreateValueDelete("a"));

            IList<object> result = change.ApplyAttributeChanges(existing);

            CollectionAssert.AreEquivalent(new object[] { "b", "c" }, result.ToArray());
        }

        [TestMethod]
        public void ApplyAttributeChanges_LongMerge()
        {
            List<object> existing = new List<object> { 1L, 2L };
            AttributeChange change = BuildExportChange("counts", AttributeModificationType.Update, AttributeType.Integer, true,
                ValueChange.CreateValueAdd(3L),
                ValueChange.CreateValueDelete(1L));

            IList<object> result = change.ApplyAttributeChanges(existing);

            CollectionAssert.AreEquivalent(new object[] { 2L, 3L }, result.ToArray());
        }

        [TestMethod]
        public void ApplyAttributeChanges_BinaryMerge()
        {
            byte[] keep = new byte[] { 1, 2, 3 };
            byte[] remove = new byte[] { 4, 5, 6 };
            byte[] add = new byte[] { 7, 8, 9 };

            List<object> existing = new List<object> { keep, remove };
            AttributeChange change = BuildExportChange("photo", AttributeModificationType.Update, AttributeType.Binary, true,
                ValueChange.CreateValueAdd(add),
                // Use a distinct-but-equal-content array to prove BinaryEqualityComparer compares by content.
                ValueChange.CreateValueDelete(new byte[] { 4, 5, 6 }));

            IList<object> result = change.ApplyAttributeChanges(existing);

            List<byte[]> resultBytes = result.Cast<byte[]>().ToList();
            Assert.AreEqual(2, resultBytes.Count);
            Assert.IsTrue(resultBytes.Any(b => b.SequenceEqual(keep)));
            Assert.IsTrue(resultBytes.Any(b => b.SequenceEqual(add)));
            Assert.IsFalse(resultBytes.Any(b => b.SequenceEqual(remove)));
        }

        [TestMethod]
        public void ApplyAttributeChanges_BooleanReplace()
        {
            List<object> existing = new List<object> { false };
            AttributeChange change = BuildExportChange("enabled", AttributeModificationType.Replace, AttributeType.Boolean, false,
                ValueChange.CreateValueAdd(true));

            IList<object> result = change.ApplyAttributeChanges(existing);

            CollectionAssert.AreEquivalent(new object[] { true }, result.ToArray());
        }

        [TestMethod]
        public void ApplyAttributeChanges_Replace_DropsAllExisting()
        {
            // Faithful to the ported logic: on Replace, every existing value is queued for delete, and the
            // delete pass runs AFTER the add pass — so the result is exactly the change's value adds, with any
            // existing value removed even if it also appears in the adds.
            List<object> existing = new List<object> { "old1", "old2" };
            AttributeChange change = BuildExportChange("member", AttributeModificationType.Replace, AttributeType.String, true,
                ValueChange.CreateValueAdd("new1"),
                ValueChange.CreateValueAdd("old1"));

            IList<object> result = change.ApplyAttributeChanges(existing);

            // "old1" is in the adds but also in existing-to-delete; delete runs last, so it is dropped.
            CollectionAssert.AreEquivalent(new object[] { "new1" }, result.ToArray());
        }

        [TestMethod]
        public void ApplyAttributeChanges_DeleteReturnsNull()
        {
            AttributeChange change = BuildExportChange("member", AttributeModificationType.Delete, AttributeType.String, true);

            IList<object> result = change.ApplyAttributeChanges(new List<object> { "a" });

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ApplyAttributeChanges_NullExistingItemsTreatedAsEmpty()
        {
            AttributeChange change = BuildExportChange("member", AttributeModificationType.Add, AttributeType.String, true,
                ValueChange.CreateValueAdd("a"),
                ValueChange.CreateValueAdd("b"));

            IList<object> result = change.ApplyAttributeChanges(null);

            CollectionAssert.AreEquivalent(new object[] { "a", "b" }, result.ToArray());
        }

        // ---------------------------------------------------------------------
        // TypeConverter.ConvertData
        // ---------------------------------------------------------------------

        [TestMethod]
        public void ConvertData_String_FromGuid()
        {
            Guid g = Guid.NewGuid();

            object result = TypeConverter.ConvertData(g, AttributeType.String);

            Assert.AreEqual(g.ToString(), result);
        }

        [TestMethod]
        public void ConvertData_Integer_FromIntAndString()
        {
            Assert.AreEqual(5L, TypeConverter.ConvertData(5, AttributeType.Integer));
            Assert.AreEqual(42L, TypeConverter.ConvertData("42", AttributeType.Integer));
        }

        [TestMethod]
        public void ConvertData_Boolean_FromStringDigits()
        {
            Assert.AreEqual(true, TypeConverter.ConvertData("1", AttributeType.Boolean));
            Assert.AreEqual(false, TypeConverter.ConvertData("0", AttributeType.Boolean));
        }

        [TestMethod]
        public void ConvertData_Binary_FromBase64String()
        {
            byte[] expected = new byte[] { 10, 20, 30 };
            string base64 = Convert.ToBase64String(expected);

            object result = TypeConverter.ConvertData(base64, AttributeType.Binary);

            CollectionAssert.AreEqual(expected, (byte[])result);
        }

        [TestMethod]
        public void ConvertData_Reference_ConvertsToString()
        {
            object result = TypeConverter.ConvertData(123L, AttributeType.Reference);

            Assert.AreEqual("123", result);
        }

        [TestMethod]
        public void ConvertData_Undefined_Throws()
        {
            Assert.Throws<NotSupportedException>(() => TypeConverter.ConvertData("x", AttributeType.Undefined));
        }

        [TestMethod]
        public void ConvertDataGeneric_Long_FromInt()
        {
            long result = TypeConverter.ConvertData<long>(7);

            Assert.AreEqual(7L, result);
        }

        [TestMethod]
        public void ConvertDataGeneric_Guid_FromString()
        {
            Guid g = Guid.NewGuid();

            Guid result = TypeConverter.ConvertData<Guid>(g.ToString());

            Assert.AreEqual(g, result);
        }

        [TestMethod]
        public void ConvertDataGeneric_Object_PassesThrough()
        {
            object input = new object();

            object result = TypeConverter.ConvertData<object>(input);

            Assert.AreSame(input, result);
        }

        // ---------------------------------------------------------------------
        // ToSmartString
        // ---------------------------------------------------------------------

        [TestMethod]
        public void ToSmartString_ByteArray_ReturnsBase64()
        {
            byte[] data = new byte[] { 1, 2, 3 };

            string result = data.ToSmartString();

            Assert.AreEqual(Convert.ToBase64String(data), result);
        }

        [TestMethod]
        public void ToSmartString_Long_ReturnsDecimal()
        {
            Assert.AreEqual("123456789", 123456789L.ToSmartString());
        }

        [TestMethod]
        public void ToSmartString_String_ReturnsItself()
        {
            Assert.AreEqual("hello", "hello".ToSmartString());
        }

        [TestMethod]
        public void ToSmartString_Bool_ReturnsBoolString()
        {
            Assert.AreEqual(true.ToString(), true.ToSmartString());
        }

        [TestMethod]
        public void ToSmartString_Guid_ReturnsGuidString()
        {
            Guid g = Guid.NewGuid();

            Assert.AreEqual(g.ToString(), g.ToSmartString());
        }

        [TestMethod]
        public void ToSmartString_DateTime_ReturnsIso8601()
        {
            DateTime dt = new DateTime(2026, 6, 15, 13, 45, 30, 123);

            string result = dt.ToSmartString();

            Assert.AreEqual(dt.ToString(GenericExtensions.ResourceManagementServiceDateFormat), result);
        }

        [TestMethod]
        public void ToSmartString_Null_ReturnsNullLiteral()
        {
            object obj = null;

            Assert.AreEqual("null", obj.ToSmartString());
        }
    }
}
