using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration related to logging.
    /// </summary>
    public class LogSettings
    {
        /// <summary>
        /// Initializes an instance of the object with default values.
        /// </summary>
        public LogSettings()
        {
            this.DebugArgs = new List<string>();
            this.LogLevel = LogLevel.Information;
        }

        /// <summary>List of categories to enable debugging information for.</summary>
        /// <remarks>A special value of "1" of the first category enables trace level debugging information for everything.</remarks>
        public List<string> DebugArgs { get; private set; }

        /// <summary>Level of logging details.</summary>
        public LogLevel LogLevel { get; private set; }

        /// <summary>
        /// Loads the logging settings from the application configuration.
        /// </summary>
        /// <param name="config">Application configuration.</param>
        /// <remarks>TODO: Currently only takes -debug arg.</remarks>
        public void Load(TextFileConfiguration config)
        {
            this.DebugArgs = config.GetOrDefault("-debug", string.Empty).Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();

            // Get the minimum log level. The default is Information.
            LogLevel minLogLevel = LogLevel.Information;
            string logLevelArg = config.GetOrDefault("-loglevel", string.Empty);
            if (!string.IsNullOrEmpty(logLevelArg))
            {
                if (!Enum.TryParse(logLevelArg, true, out minLogLevel))
                {
                    minLogLevel = LogLevel.Information;
                }
            }

            this.LogLevel = minLogLevel;
        }
    }
}