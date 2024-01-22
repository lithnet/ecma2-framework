## Lithnet ECMA2 framework

The Lithnet ECMA2 framework is a .NET library that provides a simplified interface for creating ECMA2 connectors for the Microsoft Identity Manager (MIM) synchronization engine.

It has native support for async operations, and provides a simplified interface for common operations such as creating and updating objects, and handling multivalued attributes.

The framework is available as a [NuGet package](https://www.nuget.org/packages/Lithnet.Ecma2Framework/).

## Getting started

1. Create a new .NET framework project
1. Add a reference to the `Microsoft.MetadirectoryServicesEx.dll` located on your MIM server at `C:\Program Files\Microsoft Forefront Identity Manager\2010\Synchronization Service\Bin\Assemblies`
1. Add the [Lithnet.Ecma2Framework NuGet package](https://www.nuget.org/packages/Lithnet.Ecma2Framework/) to your project
1. Create a new capability provider class, inheriting from `ICapabilitiesProvider`

```
 internal class CapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesExAsync(KeyedCollection<string, ConfigParameter> configParameters)
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
4. Create a new schema provider class, inheriting from `ISchemaProvi  der`, and provide your schema definition
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
5. Create a ConfigParametersProvider class, inheriting from `IConfigParametersProviderEx`, and provide your configuration parameters

 ```
 
internal class ConfigParametersProvider : IConfigParametersProviderEx
{
    public Task GetConfigParametersExAsync(KeyedCollection<string, ConfigParameter> existingConfigParameters, IList<ConfigParameterDefinition> newDefinitions, ConfigParameterPage page, int pageNumber)
    {
        if (pageNumber != 1)
        {
            return Task.CompletedTask;
        }

        switch (page)
        {
            case ConfigParameterPage.Connectivity:
                newDefinitions.Add(ConfigParameterDefinition.CreateStringParameter(ConfigParameterNames.TenantUrl, string.Empty));
                newDefinitions.Add(ConfigParameterDefinition.CreateEncryptedStringParameter(ConfigParameterNames.ApiKey, string.Empty));
                newDefinitions.Add(ConfigParameterDefinition.CreateDividerParameter());
                break;

            case ConfigParameterPage.Global:
                newDefinitions.Add(ConfigParameterDefinition.CreateCheckBoxParameter(ConfigParameterNames.IncludeBuiltInGroups, false));
                newDefinitions.Add(ConfigParameterDefinition.CreateCheckBoxParameter(ConfigParameterNames.IncludeAppGroups, false));
                newDefinitions.Add(ConfigParameterDefinition.CreateDividerParameter());
                newDefinitions.Add(ConfigParameterDefinition.CreateDropDownParameter(ConfigParameterNames.UserDeprovisioningAction, new string[] { "Delete", "Deactivate" }, false, "Deactivate"));
                newDefinitions.Add(ConfigParameterDefinition.CreateCheckBoxParameter(ConfigParameterNames.ActivateNewUsers, false));
                newDefinitions.Add(ConfigParameterDefinition.CreateCheckBoxParameter(ConfigParameterNames.SendActivationEmailToNewUsers, false));

                break;

            case ConfigParameterPage.Partition:
                break;

            case ConfigParameterPage.RunStep:
                break;
        }

        return Task.CompletedTask;
    }

    public Task<ParameterValidationResult> ValidateConfigParametersExAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
    {
        return Task.FromResult(new ParameterValidationResult());
    }
}
 ```

 6. Create an import provider. You can inherit from `ProducerConsumerImportProvider<T>` in order to provide a super efficient async import model. Or you can implement the `IObjectImportProvider` interface directly and provide a custom implementation.

 ```
internal class SeatImportProvider : ProducerConsumerImportProvider<SeatModel>
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private DigicertClient client;
    private ConnectionContextCustomData customData;

    protected override Task OnInitializeAsync()
    {
        this.client = this.ImportContext.ConnectionContext.GetClient();
        this.customData = this.ImportContext.GetCustomData();
        return Task.CompletedTask;
    }

    public override Task<bool> CanImportAsync(SchemaType type)
    {
        return Task.FromResult(type.Name == "seat");
    }

    protected override Task<string> GetDNAsync(SeatModel item)
    {
        return Task.FromResult($"CN={item.Seat.Identifier.EscapeLdapDnComponent()},OU={this.GetBusinessUnitName(item.Seat.BusinessUnit.Id.ToString()).EscapeLdapDnComponent()},OU=Seats");
    }

    protected override async Task<AttributeChange> CreateAttributeChangeAsync(SchemaAttribute type, ObjectModificationType objectModificationType, SeatModel item)
    {
        switch (type.Name)
        {
            case "seat_name":
                if (!string.IsNullOrWhiteSpace(item.SeatDetails.SeatName))
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.SeatDetails.SeatName);
                }
                break;

            case "email":
                if (!string.IsNullOrWhiteSpace(item.SeatDetails.Email))
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.SeatDetails.Email);
                }
                break;

            case "phone":
                if (!string.IsNullOrWhiteSpace(item.SeatDetails.Phone))
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.SeatDetails.Phone);
                }
                break;

            case "seat_creation_date":
                if (item.SeatDetails.SeatCreationDate.HasValue)
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.SeatDetails.SeatCreationDate?.UtcDateTime.ToResourceManagementServiceDateFormat());
                }
                break;

            case "seat_type_name":
                if (item.Seat.SeatTypeName.HasValue)
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.Seat.SeatTypeName.ToString());
                }
                break;

            case "active":
                if (item.Seat.Active.HasValue)
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.Seat.Active);
                }
                break;

            case "business_unit_id":
                if (item.Seat.BusinessUnit != null && item.Seat.BusinessUnit.Id != Guid.Empty)
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.Seat.BusinessUnit?.Id?.ToString());
                }
                break;

            case "account_id":
                if (item.Seat.Account != null && item.Seat.Account.Id != Guid.Empty)
                {
                    return AttributeChange.CreateAttributeAdd(type.Name, item.Seat.Account?.Id?.ToString());
                }
                break;
        }

        return null;
    }

    protected override Task<List<AnchorAttribute>> GetAnchorAttributesAsync(SeatModel item)
    {
        List<AnchorAttribute> anchors = new List<AnchorAttribute>();
        anchors.Add(AnchorAttribute.Create("id", item.Seat.Identifier));
        return Task.FromResult(anchors);
    }

    protected override async IAsyncEnumerable<SeatModel> GetObjects()
    {
        bool hasMore = false;
        int offset = 0;
        int page = 0;
        int records = 0;
        int limit = DigicertMAConfigSectionMAConfigSection.Configuration.SeatListPageSize;

        do
        {
            page++;
            logger.Trace($"Getting seats page {page}");
            var seatResponse = await this.client.Mpki.Api.V1.Seat.GetAsSeatGetResponseAsync(config =>
            {
                config.QueryParameters.Limit = limit;
                config.QueryParameters.Offset = offset;
            });

            foreach (var seat in seatResponse.Items)
            {
                records++;
                yield return new SeatModel(seat);
            }

            hasMore = seatResponse.Total > offset + limit;
            offset += limit;
        } while (hasMore);

        logger.Trace($"Seat paging completed at {page} pages and {records} records");
    }

    protected override async Task PrepareObjectForImportAsync(SeatModel item)
    {
        item.SeatDetails = await this.client.Mpki.Api.V1.Seat[item.Seat.Identifier].GetAsync(config =>
        {
            config.QueryParameters.SeatTypeIdAsGetSeatTypeIdQueryParameterType = Api.Mpki.Api.V1.Seat.Item.GetSeat_type_idQueryParameterType.USER_SEAT;
            config.QueryParameters.BusinessUnitId = item.Seat.BusinessUnit.Id;
        });
    }
    protected override Task<ObjectModificationType> GetObjectModificationTypeAsync(SeatModel seat)
    {
        return Task.FromResult(ObjectModificationType.Add);
    }

    private string GetBusinessUnitName(string id)
    {
        if (this.customData.BusinessUnits.TryGetValue(id, out string name))
        {
            return name;
        }

        return id;
    }
}

 ```



## Creating single-file assemblies
The ECMA framework provides two mechanisms for creating single-file assemblies. This is important when you have multiple management agents sharing the `Extensions` folder, and you want to avoid having your dependency DLLs overwritten by another management agent.

### ILRepack
ILRepack extracts the IL from the dependency DLLs and merges them into a single DLL. This is a robust, but heavy-handed way of creating a single-file assembly. It is also not compatible with all types of assemblies. The ECMA2 framework contains pre-configuration information for ILRepack, so all you need to do is add ILRepack to your csproj project file as follows

```
<PropertyGroup>
	<ILRepackEnabled>true</ILRepackEnabled>
</PropertyGroup>

<ItemGroup>
	<PackageReference Include="ILRepack.Lib.MSBuild.Task" Version="2.0.22" PrivateAssets="none" IncludeAssets="all"/>
</ItemGroup>

```


### Costura.Fody
Costura.Fody embeds the dependency DLLs into the main DLL as resources. The dependency DLLs are then extracted at runtime and loaded into memory. 
This preserves the original DLLs, and is compatible with all types of assemblies.

The downside of this mechanism, that that when MIM is interrogating your DLL, the dependency DLLs are not available to it. This results in errors when trying to load the DLL. To work around this issue, you need to ensure that there are no public types in your assembly. Mark them all as `internal` and it will work fine.

To use Costura.Fody, add the following to your csproj project file


```
<ItemGroup>
		<PackageReference Include="Costura.Fody" Version="5.7.0" PrivateAssets="all"/>
</ItemGroup>
```
