using System;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Lithnet.Ecma2Framework
{
    internal static class Logging
    {
        private static readonly string LogFileParameterName = "Log file";

        public static void SetupLogger(KeyedCollection<string, ConfigParameter> configParameters)
        {
            LoggingConfiguration config = new LoggingConfiguration();

            OutputDebugStringTarget traceTarget = new OutputDebugStringTarget();
            config.AddTarget("trace", traceTarget);
            traceTarget.Layout = @"${longdate}|[${threadid}]|${level:uppercase=true:padding=5}|${message}${exception:format=ToString}";

            LoggingRule rule1 = new LoggingRule("*", LogLevel.Trace, traceTarget);
            config.LoggingRules.Add(rule1);

            if (configParameters.Contains(LogFileParameterName) && !string.IsNullOrWhiteSpace(configParameters[LogFileParameterName].Value))
            {
                FileTarget fileTarget = new FileTarget();
                config.AddTarget("file", fileTarget);
                fileTarget.FileName = configParameters[LogFileParameterName].Value;
                fileTarget.Layout = "${longdate}|[${threadid}]|${level:uppercase=true:padding=5}|${message}${exception:format=ToString}";
                LoggingRule rule2 = new LoggingRule("*", LogLevel.Trace, fileTarget);
                config.LoggingRules.Add(rule2);
            }

            LogManager.Configuration = config;
            LogManager.ReconfigExistingLoggers();
        }
    }
}
