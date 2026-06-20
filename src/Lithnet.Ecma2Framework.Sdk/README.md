# Lithnet.Ecma2Framework.Sdk

The contracts a connector references. A connector author depends on this (through `Lithnet.Ecma2Framework`) and implements its interfaces.

Contents:

- Provider interfaces: `ISchemaProvider`, `IObjectImportProvider`, `IObjectExportProvider`, `IObjectPasswordProvider`, `ICapabilitiesProvider`, `IConfigParametersProvider`.
- `IEcmaStartup`: the connector's hook to configure the configuration builder and register services.
- The configuration attributes (the `[*Configuration]` classes and parameter attributes) and the context objects passed to providers.
- The DI initializer (`Ecma2Initializer`) and the init, import, export, and password orchestrators that drive the providers.

Targets netstandard2.0 so it loads in both the net48 shim build and the worker. It has no Lithnet project references; it is the base of the framework.

`Lithnet.Ecma2Framework.Hosting` hosts these orchestrators in the worker. `Lithnet.Ecma2Framework` is the package that pulls this in.
