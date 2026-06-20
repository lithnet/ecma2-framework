using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The public host-facing schema/capabilities/parameters implementation. FIM scans the public types of
    /// the per-MA shim assembly and instantiates this type (once per interface it implements) to service
    /// config-UI calls. Each interface member forwards to an internal shim connection, which marshals the
    /// call to the out-of-process worker over the named pipe.
    /// </summary>
    /// <remarks>
    /// This is plain shared source compiled into every per-MA shim (not generated). Because each shim is its
    /// own uniquely-named assembly and FIM resolves the extension type from the specific assembly named in the
    /// MA config, the fixed type name does not collide across management agents. The MA's FIM identity is the
    /// shim assembly's name (set in MSBuild); the worker is located via <see cref="WorkerPathResolver"/>.
    /// </remarks>
    public sealed class Ecma2Implementation :
        IMAExtensible2GetSchema,
        IMAExtensible2GetCapabilitiesEx,
        IMAExtensible2GetParametersEx,
        IMAExtensible2GetParameters
    {
        private readonly SchemaConnection schemaConnection;
        private readonly CapabilitiesConnection capabilitiesConnection;
        private readonly ParametersConnection parametersConnection;

        public Ecma2Implementation()
        {
            string workerPath = WorkerPathResolver.Resolve();
            this.schemaConnection = new SchemaConnection(workerPath);
            this.capabilitiesConnection = new CapabilitiesConnection(workerPath);
            this.parametersConnection = new ParametersConnection(workerPath);
        }

        Schema IMAExtensible2GetSchema.GetSchema(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return this.schemaConnection.GetSchema(configParameters);
        }

        MACapabilities IMAExtensible2GetCapabilitiesEx.GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return this.capabilitiesConnection.GetCapabilitiesEx(configParameters);
        }

        IList<ConfigParameterDefinition> IMAExtensible2GetParametersEx.GetConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return this.parametersConnection.GetConfigParametersEx(configParameters, page, pageNumber);
        }

        ParameterValidationResult IMAExtensible2GetParametersEx.ValidateConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return this.parametersConnection.ValidateConfigParametersEx(configParameters, page, pageNumber);
        }

        IList<ConfigParameterDefinition> IMAExtensible2GetParameters.GetConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return this.parametersConnection.GetConfigParametersEx(configParameters, page, 1);
        }

        ParameterValidationResult IMAExtensible2GetParameters.ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return this.parametersConnection.ValidateConfigParametersEx(configParameters, page, 1);
        }
    }
}
