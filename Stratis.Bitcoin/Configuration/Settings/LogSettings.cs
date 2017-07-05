using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Configuration.Settings
{
	public class LogSettings
    {
        public string[] DebugArgs { get; private set; }
        public LogLevel LogLevel { get; private set; }


        public void Load(TextFileConfiguration config)
		{
            this.DebugArgs = config.GetOrDefault("-debug", string.Empty).Split(',');

		    // get the minimum log level. The default is Information.
		    LogLevel minLogLevel = LogLevel.Information;
		    var logLevelArg = config.GetOrDefault("-loglevel", string.Empty);
		    if (!string.IsNullOrEmpty(logLevelArg))
		    {
		        var result = Enum.TryParse(logLevelArg, true, out minLogLevel);
		        if (!result)
		        {
		            minLogLevel = LogLevel.Information;
		        }
		    }

		    this.LogLevel = minLogLevel;
		}
    }
}