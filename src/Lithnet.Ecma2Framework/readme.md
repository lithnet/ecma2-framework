## Lithnet ECMA2 framework

The Lithnet ECMA2 framework is a .NET library that provides a simplified interface for creating ECMA2 connectors for the Microsoft Identity Manager (MIM) synchronization engine.

It has native support for async operations, and provides a simplified interface for common operations such as creating and updating objects, and handling multivalued attributes.

The framework is available as a [NuGet package](https://www.nuget.org/packages/Lithnet.Ecma2Framework/).

## Features
- Full async support
- Support for dependency injection 
- Support for `IOptions<T>` pattern for configuration with validation
- Strongly typed MIM configuration
- Implements the producer/consumer pattern for high performance imports
- Reduces the need to manage paging results back to MIM, simply add them to the `ImportContext.Items` collection
- Exports are multithreaded
- Manages object import/export exceptions automatically and reporting them back to MIM
- No need to implement IMAExtensible interfaces. The framework contains a code generator that will create these for you
- Support for single-file assemblies
- 
## Getting started

1. Create a new .NET framework project (MIM does not support .NET Core assemblies)
1. Add a reference to the `Microsoft.MetadirectoryServicesEx.dll` located on your MIM server at `C:\Program Files\Microsoft Forefront Identity Manager\2010\Synchronization Service\Bin\Assemblies`
1. Add the [Lithnet.Ecma2Framework NuGet package](https://www.nuget.org/packages/Lithnet.Ecma2Framework/) to your project
1. Create a new capability provider class, inheriting from `ICapabilitiesProvider`

```
 internal class CapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return Task.FromResult(new MACapabilities
            {
                ConcurrentOperation = true,
                DeltaImport = true,
                DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                Normalizations = MANormalizations.None,
                IsDNAsAnchor = false,
                SupportHierarchy = false,
                SupportImport = true,
                SupportPartitions = false,
                SupportPassword = true,
                ExportType = MAExportType.MultivaluedReferenceAttributeUpdate,
                ObjectConfirmation = MAObjectConfirmation.Normal,
                ObjectRename = true,
                SupportExport = true
            });
        }
    }
 ```


4. Create a new schema provider class, inheriting from `ISchemaProvider`, and provide your schema definition
 ```
internal class SchemaProvider : ISchemaProvider
{
    private static Logger logger = LogManager.GetCurrentClassLogger();
    private DigicertClient client;

    public Task<Schema> GetMmsSchemaAsync(SchemaContext context)
    {
        this.client = context.ConnectionContext.GetClient();

        Schema mmsSchema = new Schema();
        mmsSchema.Types.Add(this.GetSchemaTypeUser());

        return Task.FromResult(mmsSchema);
    }

    private SchemaType GetSchemaTypeUser()
    {
        SchemaType mmsType = SchemaType.Create("user", true);
        SchemaAttribute mmsAttribute = SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String, AttributeOperation.ImportOnly);
        mmsType.Attributes.Add(mmsAttribute);

        mmsAttribute = SchemaAttribute.CreateSingleValuedAttribute("name", AttributeType.String, AttributeOperation.ImportExport);
        mmsType.Attributes.Add(mmsAttribute);

        mmsAttribute = SchemaAttribute.CreateSingleValuedAttribute("email", AttributeType.String, AttributeOperation.ImportExport);
        mmsType.Attributes.Add(mmsAttribute);

        mmsAttribute = SchemaAttribute.CreateSingleValuedAttribute("phone", AttributeType.String, AttributeOperation.ImportExport);
        mmsType.Attributes.Add(mmsAttribute);

        return mmsType;
    }
}
 ```

 5. Create a bootstrapper class, inheriting from `IBootstrapper`, and provide your configuration
 
 ```

 ```

 
6. Create an import provider. You can inherit from `ProducerConsumerImportProvider<T>` in order to provide a super efficient async import model. Or you can implement the `IObjectImportProvider` interface directly and provide a custom implementation.
7. Create an export provider. Implement the `IObjectExportProvider` interface.
1. Optionally, expose your configuration through configuration classes. 


## Creating single-file assemblies
We recommend you use `Fody.Costura` for creating a single-file assembly. This is important when you have multiple management agents sharing the `Extensions` folder, and you want to avoid having your dependency DLLs overwritten by another management agent.


### Costura.Fody
Costura.Fody embeds the dependency DLLs into the main DLL as resources. The dependency DLLs are then extracted at runtime and loaded into memory. 
This preserves the original DLLs, and is compatible with all types of assemblies.

The downside of this mechanism, that that when MIM is interrogating your DLL, the dependency DLLs are not available to it. This can result in errors when trying to load the DLL. To work around this issue, you need to ensure that there are no public types in your assembly. Mark them all as `internal` and it will work fine.

To use Costura.Fody, add the following to your csproj project file


```
<ItemGroup>
		<PackageReference Include="Costura.Fody" Version="5.7.0" PrivateAssets="all"/>
</ItemGroup>
```
