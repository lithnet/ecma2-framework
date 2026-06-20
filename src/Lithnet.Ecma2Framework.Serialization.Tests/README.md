# Lithnet.Ecma2Framework.Serialization.Tests

Round-trip tests for `Lithnet.Ecma2Framework.Serialization`: each real `Microsoft.MetadirectoryServices` type serializes and deserializes through `MmsPipeSerializer` without loss. A completeness guard fails the build if a new field on a serialized type is not carried, so nothing is silently dropped. Targets net8.0.
