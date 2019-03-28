using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// A class representing a log rule as found in NLog.config.
    /// </summary>
    public class LogRuleModel
    {
        /// <summary>
        /// The name of the rule.
        /// </summary>
        [JsonProperty(PropertyName = "ruleName")]
        public string RuleName { get; set; }

        /// <summary>
        /// The log level.
        /// </summary>
        [JsonProperty(PropertyName = "logLevel")]
        public string LogLevel { get; set; }

        /// <summary>
        /// The full path of the log file.
        /// </summary>
        [JsonProperty(PropertyName = "filename", NullValueHandling = NullValueHandling.Ignore)]
        public string Filename { get; set; }
    }
}
