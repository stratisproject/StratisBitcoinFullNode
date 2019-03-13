using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Configuration.Logging
{
    /// <summary>
    /// An extension of the <see cref="LoggerFactory"/> that allows access to some internal components.
    /// </summary>
    public class ExtendedLoggerFactory : LoggerFactory
    {
        private const string NLogConfigFileName = "NLog.config";

        /// <summary>Configuration of console logger.</summary>
        public ConsoleLoggerSettings ConsoleSettings { get; set; }

        /// <summary>Provider of console logger.</summary>
        public ConsoleLoggerProvider ConsoleLoggerProvider { get; set; }

        /// <summary>Loads the NLog.config file from the <see cref="DataFolder"/>, if it exists.</summary>
        public void LoadNLogConfiguration(DataFolder dataFolder)
        {
            if (dataFolder == null)
                return;

            string configPath = Path.Combine(dataFolder.RootPath, NLogConfigFileName);
            if (File.Exists(configPath))
                this.ConfigureNLog(configPath);
        }
    }

    /// <summary>
    /// Integration of NLog with Microsoft.Extensions.Logging interfaces.
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>Width of a column for pretty console/log outputs.</summary>
        public const int ColumnLength = 20;

        /// <summary>Currently used node's log settings.</summary>
        private static LogSettings logSettings;

        /// <summary>Currently used data folder to determine path to logs.</summary>
        private static DataFolder folder;

        /// <summary>Mappings of keys to class name spaces to be used when filtering log categories.</summary>
        private static readonly Dictionary<string, string> KeyCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // { "addrman", "" },
            // { "cmpctblock", "" }
            // { "coindb", "" },
            // { "http", "" },
            // { "libevent", "" },
            // { "lock", "" },
            // { "mempoolrej", "" },
            { "net", $"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Connection)}.*" },
            // { "proxy", "" },
            // { "prune", "" },
            // { "rand", "" },
            // { "reindex", "" },
            // { "qt", "" },
            // { "selectcoins", "" },
            // { "tor", "" },
            // { "zmq", "" },

            // Short Names
            { "configuration", $"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Configuration)}.*" },
            { "fullnode", $"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(FullNode)}" }
        };

        public static void RegisterFeatureNamespace<T>(string key)
        {
            KeyCategories[key] = typeof(T).Namespace + ".*";
        }

        public static void RegisterFeatureClass<T>(string key)
        {
            KeyCategories[key] = typeof(T).Namespace + "." + typeof(T).Name;
        }

        /// <summary>
        /// Initializes application logging.
        /// </summary>
        static LoggingConfiguration()
        {
            // If there is no NLog.config file, we need to initialize the configuration ourselves.
            if (LogManager.Configuration == null) LogManager.Configuration = new NLog.Config.LoggingConfiguration();

            // Installs handler to be called when NLog's configuration file is changed on disk.
            LogManager.ConfigurationReloaded += NLogConfigurationReloaded;
        }

        /// <summary>
        /// Event handler to be called when logging <see cref="NLog.LogManager.Configuration"/> gets reloaded.
        /// </summary>
        /// <param name="sender">Not used.</param>
        /// <param name="e">Not used.</param>
        public static void NLogConfigurationReloaded(object sender, LoggingConfigurationReloadedEventArgs e)
        {
            AddFilters(logSettings, folder);
        }

        /// <summary>
        /// Extends the logging rules in the "NLog.config" with node log settings rules.
        /// </summary>
        /// <param name="settings">Node log settings to extend the rules from the configuration file, or null if no extension is required.</param>
        /// <param name="dataFolder">Data folder to determine path to log files.</param>
        private static void AddFilters(LogSettings settings = null, DataFolder dataFolder = null)
        {
            if (settings == null) return;

            logSettings = settings;
            folder = dataFolder;

            // If we use "debug*" targets, which are defined in "NLog.config", make sure they log into the correct log folder in data directory.
            List<Target> debugTargets = LogManager.Configuration.AllTargets.Where(t => (t.Name != null) && t.Name.StartsWith("debug")).ToList();
            foreach (Target debugTarget in debugTargets)
            {
                FileTarget debugFileTarget = debugTarget is AsyncTargetWrapper ? (FileTarget)((debugTarget as AsyncTargetWrapper).WrappedTarget) : (FileTarget)debugTarget;
                string currentFile = debugFileTarget.FileName.Render(new LogEventInfo { TimeStamp = DateTime.UtcNow });
                debugFileTarget.FileName = Path.Combine(folder.LogPath, Path.GetFileName(currentFile));

                if (debugFileTarget.ArchiveFileName != null)
                {
                    string currentArchive = debugFileTarget.ArchiveFileName.Render(new LogEventInfo {TimeStamp = DateTime.UtcNow});
                    debugFileTarget.ArchiveFileName = Path.Combine(folder.LogPath, currentArchive);
                }
            }

            // Remove rule that forbids logging before the logging is initialized.
            LoggingRule nullPreInitRule = null;
            foreach (LoggingRule rule in LogManager.Configuration.LoggingRules)
            {
                if (rule.Final && rule.NameMatches("*") && (rule.Targets.Count > 0) && (rule.Targets[0].Name == "null"))
                {
                    nullPreInitRule = rule;
                    break;
                }
            }

            LogManager.Configuration.LoggingRules.Remove(nullPreInitRule);

            // Configure main file target, configured using command line or node configuration file settings.
            var mainTarget = new FileTarget
            {
                Name = "main",
                FileName = Path.Combine(folder.LogPath, "node.txt"),
                ArchiveFileName = Path.Combine(folder.LogPath, "node-${date:universalTime=true:format=yyyy-MM-dd}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.Sequence,
                ArchiveEvery = FileArchivePeriod.Day,
                MaxArchiveFiles = 7,
                Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}",
                Encoding = Encoding.UTF8
            };

            LogManager.Configuration.AddTarget(mainTarget);

            // Default logging level is Info for all components.
            var defaultRule = new LoggingRule($"{nameof(Stratis)}.{nameof(Bitcoin)}.*", settings.LogLevel, mainTarget);

            if (settings.DebugArgs.Any() && settings.DebugArgs[0] != "1")
            {
                var usedCategories = new HashSet<string>(StringComparer.Ordinal);

                // Increase selected categories to Debug.
                foreach (string key in settings.DebugArgs)
                {
                    if (!KeyCategories.TryGetValue(key.Trim(), out string category))
                    {
                        // Allow direct specification - e.g. "-debug=Stratis.Bitcoin.Miner".
                        category = key.Trim();
                    }

                    if (!usedCategories.Contains(category))
                    {
                        usedCategories.Add(category);
                        var rule = new LoggingRule(category, settings.LogLevel, mainTarget);
                        LogManager.Configuration.LoggingRules.Add(rule);
                    }
                }
            }

            LogManager.Configuration.LoggingRules.Add(defaultRule);

            // Apply new rules.
            LogManager.ReconfigExistingLoggers();
        }

        /// <summary>
        /// Extends the logging rules in the "NLog.config" with node log settings rules.
        /// </summary>
        /// <param name="loggerFactory">Not used.</param>
        /// <param name="settings">Node log settings to extend the rules from the configuration file, or null if no extension is required.</param>
        /// <param name="dataFolder">Data folder to determine path to log files.</param>
        public static void AddFilters(this ILoggerFactory loggerFactory, LogSettings settings, DataFolder dataFolder)
        {
            AddFilters(settings, dataFolder);
        }

        /// <summary>
        /// Configure the console logger and set it to filter logs not related to the fullnode.
        /// </summary>
        /// <param name="loggerFactory">The logger factory to add the console logger.</param>
        public static void AddConsoleWithFilters(this ILoggerFactory loggerFactory)
        {
            var consoleLoggerSettings = new ConsoleLoggerSettings
            {
                Switches =
                {
                    {"Default", Microsoft.Extensions.Logging.LogLevel.Information},
                    {"System", Microsoft.Extensions.Logging.LogLevel.Warning},
                    {"Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning},
                    {"Microsoft.AspNetCore", Microsoft.Extensions.Logging.LogLevel.Error}
                }
            };

            var consoleLoggerProvider = new ConsoleLoggerProvider(consoleLoggerSettings);
            loggerFactory.AddProvider(consoleLoggerProvider);

            var extendedLoggerFactory = loggerFactory as ExtendedLoggerFactory;
            Guard.NotNull(extendedLoggerFactory, nameof(extendedLoggerFactory));
            extendedLoggerFactory.ConsoleLoggerProvider = consoleLoggerProvider;
            extendedLoggerFactory.ConsoleSettings = consoleLoggerSettings;
        }

        /// <summary>
        /// Configure the console logger and set it to filter logs not related to the fullnode.
        /// </summary>
        /// <param name="loggerFactory">Not used.</param>
        /// <param name="consoleLoggerSettings">Console settings to filter.</param>
        /// <param name="settings">Settings that hold potential debug arguments, if null no debug arguments will be loaded."/></param>
        public static void ConfigureConsoleFilters(this ILoggerFactory loggerFactory, ConsoleLoggerSettings consoleLoggerSettings, LogSettings settings)
        {
            if (settings != null)
            {
                if (settings.DebugArgs.Any())
                {
                    if (settings.DebugArgs[0] == "1")
                    {
                        // Increase all logging to Debug.
                        consoleLoggerSettings.Switches.Add($"{nameof(Stratis)}.{nameof(Bitcoin)}", Microsoft.Extensions.Logging.LogLevel.Debug);
                    }
                    else
                    {
                        var usedCategories = new HashSet<string>(StringComparer.Ordinal);

                        // Increase selected categories to Debug.
                        foreach (string key in settings.DebugArgs)
                        {
                            if (!KeyCategories.TryGetValue(key.Trim(), out string category))
                            {
                                // Allow direct specification - e.g. "-debug=Stratis.Bitcoin.Miner".
                                category = key.Trim();
                            }

                            if (!usedCategories.Contains(category))
                            {
                                usedCategories.Add(category);
                                consoleLoggerSettings.Switches.Add(category.TrimEnd('*').TrimEnd('.'), Microsoft.Extensions.Logging.LogLevel.Debug);
                            }
                        }
                    }
                }
            }

            consoleLoggerSettings.Reload();
        }

        /// <summary>
        /// Obtains configuration of the console logger.
        /// </summary>
        /// <param name="loggerFactory">Logger factory interface being extended.</param>
        /// <returns>Console logger settings.</returns>
        public static ConsoleLoggerSettings GetConsoleSettings(this ILoggerFactory loggerFactory)
        {
            var extendedLoggerFactory = loggerFactory as ExtendedLoggerFactory;
            Guard.NotNull(extendedLoggerFactory, nameof(extendedLoggerFactory));
            return extendedLoggerFactory.ConsoleSettings;
        }

        /// <summary>
        /// Obtains configuration of the console logger provider.
        /// </summary>
        /// <param name="loggerFactory">Logger factory interface being extended.</param>
        /// <returns>Console logger provider.</returns>
        public static ConsoleLoggerProvider GetConsoleLoggerProvider(this ILoggerFactory loggerFactory)
        {
            var extendedLoggerFactory = loggerFactory as ExtendedLoggerFactory;
            Guard.NotNull(extendedLoggerFactory, nameof(extendedLoggerFactory));
            return extendedLoggerFactory.ConsoleLoggerProvider;
        }
    }
}
