using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;
using NLog.Config;

namespace Lithnet.Ecma2Framework
{
    public interface ISettingsProvider
    {
        string ManagementAgentName { get; }

        bool HandleOwnLogConfiguration { get; }

        LoggingConfiguration GetCustomLogConfiguration(KeyedCollection<string, ConfigParameter> configParameters);
    }
}