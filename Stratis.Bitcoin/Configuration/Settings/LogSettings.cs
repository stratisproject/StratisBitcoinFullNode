using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Configuration.Settings
{
	public class LogSettings
	{
	    public LogSettings()
	    {
	        this.DebugArgs = new List<string>();
	        this.LogLevel = LogLevel.Information;
        }

        public List<string> DebugArgs { get; private set; }
        public LogLevel LogLevel { get; private set; }


        public void Load(TextFileConfiguration config)
		{
            this.DebugArgs = config.GetOrDefault("-debug", string.Empty).Split(',').ToList();

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