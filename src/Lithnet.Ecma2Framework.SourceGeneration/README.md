# Lithnet.Ecma2Framework.SourceGeneration

The Roslyn source generator that runs during the host build, not the connector's own compile. It reads the connector's referenced assembly, finds the `IEcmaStartup` implementation and the providers, and emits:

- the worker entry point (`Main`) that constructs the startup and calls `WorkerEntryPoint.RunAsync`,
- the config registration provider that maps MIM config parameters to typed options,
- the runtime resolver that loads `Microsoft.MetadirectoryServicesEx` from the MIM install, with a `ModuleInitializerAttribute` polyfill emitted for net48 hosts.

It raises diagnostics (ECMA2xxx) when a required provider or the startup class is missing or not public.

Targets netstandard2.0 and ships inside the `Lithnet.Ecma2Framework` package, referenced as an analyzer by the host build. The templates for the emitted sources are in `Templates/`.
