using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for classes and interfaces related to logging.
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Creates a new <see cref="ILogger"/> instance which prefixes every log with specified string.
        /// </summary>
        /// <param name="loggerFactory">Logger factory interface being extended.</param>
        /// <param name="categoryName">Category name for messages produced by the logger.</param>
        /// <param name="prefix">String to be put in front of each log of the newly created logger.</param>
        /// <returns>Newly created logger.</returns>
        public static ILogger CreateLogger(this ILoggerFactory loggerFactory, string categoryName, string prefix = "")
        {
            return new PrefixLogger(categoryName, prefix);
        }

        /// <summary>
        /// Converts <see cref="Microsoft.Extensions.Logging.LogLevel"/> to <see cref="NLog.LogLevel">.
        /// </summary>
        /// <param name="LogLevel">Log level value to convert.</param>
        /// <returns>NLog value of the log level.</returns>
        public static NLog.LogLevel ToNLogLevel(this LogLevel LogLevel)
        {
            NLog.LogLevel res = NLog.LogLevel.Trace;

            switch (LogLevel)
            {
                case LogLevel.Trace: res = NLog.LogLevel.Trace; break;
                case LogLevel.Debug: res = NLog.LogLevel.Debug; break;
                case LogLevel.Information: res = NLog.LogLevel.Info; break;
                case LogLevel.Warning: res = NLog.LogLevel.Warn; break;
                case LogLevel.Error: res = NLog.LogLevel.Error; break;
                case LogLevel.Critical: res = NLog.LogLevel.Fatal; break;
            }

            return res;
        }
    }
}
