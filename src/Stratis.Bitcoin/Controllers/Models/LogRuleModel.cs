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
        public string RuleName { get; set; }

        /// <summary>
        /// The log level.
        /// </summary>
        public string LogLevel { get; set; }

        /// <summary>
        /// The full path of the log file.
        /// </summary>
        public string FileName { get; set; }
    }
}
