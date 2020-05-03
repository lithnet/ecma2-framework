using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.MetadirectoryServices;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Lithnet.Ecma2Framework
{
    internal static class Logging
    {
        internal static readonly string LogFileParameterName = "Log file";

        internal static readonly string LogLevelParameterName = "Log level";

        internal static readonly string LogDaysParameterName = "Number of days to retain logs";

        internal static void AddBuiltInLoggingParameters(ConfigParameterPage page, List<ConfigParameterDefinition> configParameterDefinitions)
        {
            if (page == ConfigParameterPage.Connectivity)
            {
                ISettingsProvider settings = InterfaceManager.GetProviderOrDefault<ISettingsProvider>();

                if (!settings?.HandleOwnLogConfiguration ?? false)
                {
                    configParameterDefinitions.Add(ConfigParameterDefinition.CreateStringParameter(Logging.LogFileParameterName, string.Empty));
                    configParameterDefinitions.Add(ConfigParameterDefinition.CreateDropDownParameter(Logging.LogLevelParameterName, LogLevel.AllLevels.Reverse().Select(t => t.Name).ToArray(), false, "Info"));
                    configParameterDefinitions.Add(ConfigParameterDefinition.CreateStringParameter(Logging.LogDaysParameterName, string.Empty));
                    configParameterDefinitions.Add(ConfigParameterDefinition.CreateDividerParameter());
                }
            }
        }

        internal static ParameterValidationResult ValidateBuiltInLoggingParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            if (page == ConfigParameterPage.Connectivity)
            {
                ISettingsProvider settings = InterfaceManager.GetProviderOrDefault<ISettingsProvider>();

                if (!settings?.HandleOwnLogConfiguration ?? false)
                {
                    if (!string.IsNullOrWhiteSpace(configParameters[Logging.LogDaysParameterName].Value) && !int.TryParse(configParameters[Logging.LogDaysParameterName].Value, out int _))
                    {
                        return new ParameterValidationResult(ParameterValidationResultCode.Failure, "The value for log retention needs to be a number", Logging.LogDaysParameterName);
                    }

                    if (!string.IsNullOrWhiteSpace(configParameters[Logging.LogLevelParameterName].Value))
                    {
                        try
                        {
                            LogLevel.FromString(configParameters[Logging.LogLevelParameterName].Value);
                        }
                        catch (Exception)
                        {
                            return new ParameterValidationResult(ParameterValidationResultCode.Failure, "The value for log level was unknown", Logging.LogLevelParameterName);
                        }
                    }
                }
            }

            return null;
        }

        public static void SetupLogger(KeyedCollection<string, ConfigParameter> configParameters)
        {
            var settingsProvider = InterfaceManager.GetProviderOrDefault<ISettingsProvider>();
            LoggingConfiguration logConfiguration;

            // Implementation will handle its own logging
            if (settingsProvider?.HandleOwnLogConfiguration ?? false)
            {
                logConfiguration = settingsProvider?.GetCustomLogConfiguration(configParameters);
                LogManager.Configuration = logConfiguration;
                LogManager.ReconfigExistingLoggers();
                return;
            }

            logConfiguration = new LoggingConfiguration();

            LogLevel level = LogLevel.Info;

            if (configParameters.Contains(LogLevelParameterName))
            {
                string value = configParameters[LogLevelParameterName].Value;

                if (value != null)
                {
                    level = LogLevel.FromString(value);
                }
            }

            if (level == LogLevel.Trace)
            {
                OutputDebugStringTarget odsTarget = new OutputDebugStringTarget();
                logConfiguration.AddTarget("ods", odsTarget);
                LoggingRule odsRule = new LoggingRule("*", level, odsTarget);
                logConfiguration.LoggingRules.Add(odsRule);
            }

            EventLogTarget eventLogTarget = new EventLogTarget();
            eventLogTarget.Source = settingsProvider?.ManagementAgentName ?? "Lithnet Ecma2 Framework";
            eventLogTarget.Category = settingsProvider?.ManagementAgentName ?? "Lithnet Ecma2 Framework";
            eventLogTarget.Layout = "${message}${newline}${exception:format=ToString}";
            LoggingRule eventLogRule = new LoggingRule("*", LogLevel.Warn, eventLogTarget);
            logConfiguration.LoggingRules.Add(eventLogRule);

            if (configParameters.Contains(LogFileParameterName) && !string.IsNullOrWhiteSpace(configParameters[LogFileParameterName].Value))
            {
                FileTarget fileTarget = new FileTarget();
                logConfiguration.AddTarget("file", fileTarget);
                fileTarget.FileName = configParameters[LogFileParameterName].Value;
                fileTarget.Layout = "${longdate}|[${threadid:padding=4}]|${level:uppercase=true:padding=5}|${message}${exception:format=ToString}";
                fileTarget.ArchiveEvery = FileArchivePeriod.Day;
                fileTarget.ArchiveNumbering = ArchiveNumberingMode.Date;

                if (configParameters.Contains(LogDaysParameterName) && int.TryParse(configParameters[LogDaysParameterName].Value, out int days))
                {
                    fileTarget.MaxArchiveFiles = days;
                }
                else
                {
                    fileTarget.MaxArchiveFiles = 7;
                }

                LoggingRule rule2 = new LoggingRule("*", level, fileTarget);
                logConfiguration.LoggingRules.Add(rule2);
            }

            LogManager.Configuration = logConfiguration;
            LogManager.ReconfigExistingLoggers();
        }
    }
}