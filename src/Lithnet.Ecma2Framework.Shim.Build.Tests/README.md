# Lithnet.Ecma2Framework.Shim.Build.Tests

Builds a shim through the real build mechanism (`Shim.proj`) for a sample management agent name, then inspects the produced DLL by reading its metadata, without executing it. It asserts the assembly name matches the management agent name, the public types are exactly the host-facing implementations, each declares the expected `IMAExtensible2*` interface, and the reference closure contains no Lithnet assemblies. Targets net8.0; it reads the net48 shim's metadata but never loads it.
