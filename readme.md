# Lithnet ECMA2 Framework

The Lithnet ECMA2 Framework is a .NET library for building ECMA 2.2 connectors (management agents) for the Microsoft Identity Manager (MIM) synchronization engine. You implement a few provider interfaces for schema, import, export, and password operations, and the framework builds the connector and runs it.

The framework is available as a [NuGet package](https://www.nuget.org/packages/Lithnet.Ecma2Framework/).

## Features

- Full async support.
- Out-of-process hosting, meaning no DLL conflicts between MIM or other management agents.
- Support for .NET 8.0 and later, as well as .NET Framework 4.8 and later.
- Dependency injection, and the `IOptions<T>` configuration pattern with validation.
- Strongly typed access to the MIM configuration parameters.
- Producer/consumer imports, so you do not manage paging of results back to MIM.
- Multithreaded exports by default.
- Import and export exceptions are handled and reported back to MIM.
- No need to implement the `IMAExtensible2*` interfaces. The framework generates them from your providers.

## Documentation and getting started

See https://docs.lithnet.io/ecma2-framework for installation and a getting-started guide.
