using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Stratis.Bitcoin.Configuration.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.Configuration.Logging
{
    /// <summary>
    /// Integration of NLog with Microsoft.Extensions.Logging interfaces.
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>Width of a column for pretty console/log outputs.</summary>
        public const int ColumnLength = 16;

        /// <summary>Currently used node's log settings.</summary>
        private static LogSettings logSettings;

        /// <summary>Currently used data folder to determine path to logs.</summary>
        private static DataFolder folder;

        /// <summary>Mappings of keys to class name spaces to be used when filtering log categories.</summary>
        private static readonly Dictionary<string, string> keyCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            //{ "addrman", "" },
            //{ "alert", "" },
            { "bench", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Features)}.{nameof(Stratis.Bitcoin.Features.Consensus)}.{nameof(Stratis.Bitcoin.Features.Consensus.ConsensusStats)}" },
            //{ "cmpctblock", "" }
            //{ "coindb", "" },
            { "db", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Features)}.{nameof(Stratis.Bitcoin.Features.BlockStore)}.*"}, 
            //{ "http", "" }, 
            //{ "libevent", "" }, 
            //{ "lock", "" }, 
            { "mempool", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Features)}.{nameof(Stratis.Bitcoin.Features.MemoryPool)}.*" }, 
            //{ "mempoolrej", "" }, 
            { "net", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Connection)}.*" }, 
            //{ "proxy", "" }, 
            //{ "prune", "" }, 
            //{ "rand", "" }, 
            //{ "reindex", "" }, 
            //{ "qt", "" },
            //{ "selectcoins", "" }, 
            //{ "tor", "" }, 
            //{ "zmq", "" }, 
            
            // Short Names
            { "estimatefee", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Features)}.{nameof(Stratis.Bitcoin.Features.MemoryPool)}.{nameof(Stratis.Bitcoin.Features.MemoryPool.Fee)}.*" },
            { "configuration", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Configuration)}.*" },
            { "fullnode", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.FullNode)}" },
            { "consensus", $"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.{nameof(Stratis.Bitcoin.Features)}.{nameof(Stratis.Bitcoin.Features.Consensus)}.*" },
        };

        public static void RegisterFeatureNamespace<T>(string key)
        {
            keyCategories[key] = typeof(T).Namespace + ".*";
        }

        /// <summary>Configuration of console logger.</summary>
        private static ConsoleLoggerSettings consoleSettings;

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

            // If we use debugFile target, which is defined in "NLog.config", we make sure it is in the log folder.
            Target debugTarget = LogManager.Configuration.FindTargetByName("debugFile");
            if (debugTarget != null)
            {
                FileTarget debugFileTarget = debugTarget is AsyncTargetWrapper ? (FileTarget)((debugTarget as AsyncTargetWrapper).WrappedTarget) : (FileTarget)debugTarget;
                string currentFile = debugFileTarget.FileName.Render(new LogEventInfo { TimeStamp = DateTime.UtcNow });
                debugFileTarget.FileName = Path.Combine(folder.LogPath, Path.GetFileName(currentFile));
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
            var mainTarget = new FileTarget();
            mainTarget.Name = "main";
            mainTarget.FileName = Path.Combine(folder.LogPath, "node.txt");
            mainTarget.ArchiveFileName = Path.Combine(folder.LogPath, "node-${date:universalTime=true:format=yyyy-MM-dd}.txt");
            mainTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
            mainTarget.ArchiveEvery = FileArchivePeriod.Day;
            mainTarget.MaxArchiveFiles = 7;
            mainTarget.Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}";
            mainTarget.Encoding = Encoding.UTF8;

            LogManager.Configuration.AddTarget(mainTarget);

            // Default logging level is Info for all components.
            var defaultRule = new LoggingRule($"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.*", NLog.LogLevel.Info, mainTarget);

            if (settings.DebugArgs.Any())
            {
                if (settings.DebugArgs[0] == "1")
                {
                    // Increase all logging to Debug level.
                    defaultRule = new LoggingRule($"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}.*", NLog.LogLevel.Debug, mainTarget);
                }
                else
                {
                    HashSet<string> usedCategories = new HashSet<string>(StringComparer.Ordinal);

                    // Increase selected categories to Debug.
                    foreach (string key in settings.DebugArgs)
                    {
                        string category;
                        if (!keyCategories.TryGetValue(key.Trim(), out category))
                        {
                            // Allow direct specification - e.g. "-debug=Stratis.Bitcoin.Miner".
                            category = key.Trim();
                        }

                        if (!usedCategories.Contains(category))
                        {
                            usedCategories.Add(category);
                            var rule = new LoggingRule(category, NLog.LogLevel.Debug, mainTarget);
                            LogManager.Configuration.LoggingRules.Add(rule);
                        }
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
        /// <param name="consoleLoggerSettings"></param>
        /// <returns>The new console settings.</returns>
        public static void AddConsoleWithFilters(this ILoggerFactory loggerFactory, out ConsoleLoggerSettings consoleLoggerSettings)
        {
            consoleLoggerSettings = new ConsoleLoggerSettings
            {
                Switches =
                {
                    {"Default", Microsoft.Extensions.Logging.LogLevel.Information},
                    {"System", Microsoft.Extensions.Logging.LogLevel.Warning},
                    {"Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning},
                    {"Microsoft.AspNetCore", Microsoft.Extensions.Logging.LogLevel.Error}
                }
            };

            loggerFactory.AddConsole(consoleLoggerSettings);
            consoleSettings = consoleLoggerSettings;
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
                        consoleLoggerSettings.Switches.Add($"{nameof(Stratis)}.{nameof(Stratis.Bitcoin)}", Microsoft.Extensions.Logging.LogLevel.Debug);
                    }
                    else
                    {
                        HashSet<string> usedCategories = new HashSet<string>(StringComparer.Ordinal);

                        // Increase selected categories to Debug.
                        foreach (string key in settings.DebugArgs)
                        {
                            string category;
                            if (!keyCategories.TryGetValue(key.Trim(), out category))
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
            return consoleSettings;
        }
    }
}
