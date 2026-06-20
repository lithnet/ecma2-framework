# Lithnet.Ecma2Framework

The package a connector installs. It is all a connector needs: it depends on `Lithnet.Ecma2Framework.Sdk` and `Lithnet.Ecma2Framework.Hosting`, bundles the source generator, and carries the MSBuild targets that turn the connector library into a running management agent.

Its content is the `build/` folder, which ships in the package and runs in the connector's build:

- **Lithnet.Ecma2Framework.props / .targets**: imported into the connector project. They derive the shim name and the host MMS path, then build the two extra outputs after the connector builds.
- **Host.proj**: builds the worker host executable. It references the connector by project reference, runs the source generator, and compiles the generated `Main` plus the connector's closure. The executable targets the connector's own framework.
- **Shim.proj**: builds the net48 shim DLL from the shared shim source, keeping its reference closure to the host MMS assembly, the BCL, and CsWin32.
- **pack-local.ps1**: packs the framework packages as a local prerelease for side-by-side connector development.

`Microsoft.MetadirectoryServicesEx` is Microsoft's assembly and is never shipped. The targets reference it for compilation only, and the worker loads the customer's own copy at runtime.

Targets netstandard2.0. The package version comes from `VersionPrefix`.
