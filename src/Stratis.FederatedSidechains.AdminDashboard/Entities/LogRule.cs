using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Stratis.FederatedSidechains.AdminDashboard.Entities
{
    public class LogRule
    {
        [JsonProperty("ruleName")]
        public string Name { get; set; }

        [JsonProperty("logLevel")]
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel MinLevel { get; set; } = LogLevel.Trace;

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonIgnore]
        public LogLevel StratisActualLevel { get; set; } = LogLevel.Trace;

        [JsonIgnore]
        public LogLevel SidechainActualLevel { get; set; } = LogLevel.Trace;
    }

    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
}
