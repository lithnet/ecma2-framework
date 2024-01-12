using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lithnet.Ecma2Framework
{
    [Generator]
    public class EcmaGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("Ecma2ImportImplementation.g.cs", SourceText.From(@"
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2ImportImplementation : IMAExtensible2CallImport
    {
        private Ecma2Import provider;

        public Ecma2ImportImplementation()
        {
            this.provider = new Ecma2Import();
        }

        int IMAExtensible2CallImport.ImportDefaultPageSize => 100;

        int IMAExtensible2CallImport.ImportMaxPageSize => 9999;

        OpenImportConnectionResults IMAExtensible2CallImport.OpenImportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            return AsyncHelper.RunSync(this.provider.OpenImportConnectionAsync(configParameters, types, importRunStep));
        }
        GetImportEntriesResults IMAExtensible2CallImport.GetImportEntries(GetImportEntriesRunStep importRunStep)
        {
            return AsyncHelper.RunSync(this.provider.GetImportEntriesPageAsync());
        }

        CloseImportConnectionResults IMAExtensible2CallImport.CloseImportConnection(CloseImportConnectionRunStep importRunStep)
        {
            return AsyncHelper.RunSync(this.provider.CloseImportConnectionAsync(importRunStep));
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2ExportImplementation.g.cs", SourceText.From(@"
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2ExportImplementation : IMAExtensible2CallExport
    {
        private Ecma2Export provider;

        public Ecma2ExportImplementation()
        {
            this.provider = new Ecma2Export();
        }

        int IMAExtensible2CallExport.ExportDefaultPageSize => 100;

        int IMAExtensible2CallExport.ExportMaxPageSize => 9999;

        void IMAExtensible2CallExport.OpenExportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            AsyncHelper.RunSync(this.provider.OpenExportConnectionAsync(configParameters, types, exportRunStep));
        }

        PutExportEntriesResults IMAExtensible2CallExport.PutExportEntries(IList<CSEntryChange> csentries)
        {
            return AsyncHelper.RunSync(this.provider.PutExportEntriesAsync(csentries));
        }

        void IMAExtensible2CallExport.CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
            AsyncHelper.RunSync(this.provider.CloseExportConnectionAsync(exportRunStep));
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2PasswordImplementation.g.cs", SourceText.From(@"
using System.Collections.ObjectModel;
using System.Security;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2PasswordImplementation : IMAExtensible2Password
    {
        private Ecma2Password provider;

        public Ecma2PasswordImplementation()
        {
            this.provider = new Ecma2Password();
        }

        void IMAExtensible2Password.OpenPasswordConnection(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            AsyncHelper.RunSync(this.provider.OpenPasswordConnectionAsync(configParameters, partition));
        }

        void IMAExtensible2Password.ClosePasswordConnection()
        {
            AsyncHelper.RunSync(this.provider.ClosePasswordConnectionAsync());
        }

        ConnectionSecurityLevel IMAExtensible2Password.GetConnectionSecurityLevel()
        {
            return AsyncHelper.RunSync(this.provider.GetConnectionSecurityLevelAsync());
        }

        void IMAExtensible2Password.SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            AsyncHelper.RunSync(this.provider.SetPasswordAsync(csentry, newPassword, options));
        }

        void IMAExtensible2Password.ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            AsyncHelper.RunSync(this.provider.ChangePasswordAsync(csentry, oldPassword, newPassword));
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2Implementation.g.cs", SourceText.From(@"
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Implementation :
        IMAExtensible2GetSchema,
        IMAExtensible2GetCapabilitiesEx,
        IMAExtensible2GetParametersEx,
        IMAExtensible2GetParameters
    {
        private Ecma2 provider;

        public Ecma2Implementation()
        {
            this.provider = new Ecma2();
        }


        MACapabilities IMAExtensible2GetCapabilitiesEx.GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return AsyncHelper.RunSync(this.provider.GetCapabilitiesExAsync(configParameters));
        }

        IList<ConfigParameterDefinition> IMAExtensible2GetParametersEx.GetConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return AsyncHelper.RunSync(this.provider.GetConfigParametersExAsync(configParameters, page, pageNumber));
        }

        ParameterValidationResult IMAExtensible2GetParametersEx.ValidateConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return AsyncHelper.RunSync(this.provider.ValidateConfigParametersAsync(configParameters, page, pageNumber));
        }

        Schema IMAExtensible2GetSchema.GetSchema(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return AsyncHelper.RunSync(this.provider.GetSchemaAsync(configParameters));
        }

        IList<ConfigParameterDefinition> IMAExtensible2GetParameters.GetConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return AsyncHelper.RunSync(this.provider.GetConfigParametersExAsync(configParameters, page, 1));
        }

        ParameterValidationResult IMAExtensible2GetParameters.ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return AsyncHelper.RunSync(this.provider.ValidateConfigParametersAsync(configParameters, page, 1));
        }
    }
}
", Encoding.UTF8));
        }
    }
}
