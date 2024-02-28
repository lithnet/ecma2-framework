# Lithnet ECMA2 framework

The Lithnet ECMA2 framework is a .NET library that provides a simplified interface for creating ECMA2 connectors for the Microsoft Identity Manager (MIM) synchronization engine.

It has native support for async operations, and provides a simplified interface for common operations such as creating and updating objects, and handling multivalued attributes.

The framework is available as a [NuGet package](https://www.nuget.org/packages/Lithnet.Ecma2Framework/).

## Features
- Full async support
- Support for dependency injection 
- Support for `IOptions<T>` pattern for configuration with validation
- Strongly typed MIM configuration
- Implements the producer/consumer pattern for high performance imports
- Removes the need to manage paging of import results back to MIM
- Exports are multithreaded by default
- Manages object import/export exceptions automatically and reports them back to MIM
- No need to implement `IMAExtensible*` interfaces. The framework contains a code generator that will create these for you
- Support for single-file assemblies

## Documentation and getting started

Visit our documentation site at https://docs.lithnet.io/ecma2-framework for more information on how to get started with the Lithnet ECMA2 framework.