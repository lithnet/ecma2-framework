using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;
using NLog.Config;

namespace Lithnet.Ecma2Framework
{
    public interface ISettingsProvider
    {
        string ManagementAgentName { get; }

        bool HandleOwnLogConfiguration { get; }

        Task<LoggingConfiguration> GetCustomLogConfigurationAsync(KeyedCollection<string, ConfigParameter> configParameters);
    }
}