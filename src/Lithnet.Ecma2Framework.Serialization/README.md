# Lithnet.Ecma2Framework.Serialization

Serializes real `Microsoft.MetadirectoryServices` graphs (`CSEntryChange`, `Schema`, `MACapabilities`, and the run-step types) so they can cross the named pipe between the shim and the worker. `MmsPipeSerializer` is a `DataContractSerializer` rooted at the real abstract types, with `*Serializable` surrogate DTOs as known types and a serialization surrogate attached. Real objects round-trip without loss; there is no separate mirror object model.

It has no public API. The assembly is bundled into `Lithnet.Ecma2Framework.Hosting` (the worker). The same source is compiled into the net48 shim, where the public types are made internal by the `ECMA2_SHIM_INTERNAL` compile constant.

Targets netstandard2.0 and references the host MMS assembly only.
