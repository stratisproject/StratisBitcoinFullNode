using Microsoft.Extensions.Logging;
using System;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Logger that prefixes every log with specified string.
    /// </summary>
    /// <remarks>
    /// TODO: Currently this is not compatible with logging to the console.
    /// This means that if you use a prefix logger for logging in a class,
    /// the logging output will not go to the console even if the logging 
    /// level is at or above the minimum logging level for the console.
    /// </remarks>
    public class PrefixLogger : ILogger
    {
        /// <summary>Internal logger instance.</summary>
        //private ILogger logger;
        private NLog.Logger logger;

        /// <summary>Prefix to put in front of every message.</summary>
        private string prefix;

        /// <summary>Wrapper class type for the NLog callsite to skip it.</summary>
        private Type wrapperType;

        /// <summary>
        /// Creates a logger instance with given prefix.
        /// </summary>
        /// <param name="categoryName">Category name for messages produced by the logger.</param>
        /// <param name="prefix">String to be put in front of each log of the newly created logger.</param>
        public PrefixLogger(string categoryName, string prefix = null)
        {
            this.logger = NLog.LogManager.GetLogger(categoryName);
            this.prefix = prefix != null ? prefix : "";
            this.wrapperType = typeof(PrefixLogger);
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return NLog.NestedDiagnosticsLogicalContext.Push(state);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return this.IsEnabled(logLevel.ToNLogLevel());
        }

        /// <summary>
        /// Checks if the given log level is enabled.
        /// </summary>
        /// <param name="logLevel">Log level to check.</param>
        /// <returns><c>true</c> if the log level is enabled, <c>false</c> otherwise.</returns>
        private bool IsEnabled(NLog.LogLevel logLevel)
        {
            return this.logger.IsEnabled(logLevel);
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            NLog.LogLevel nLogLevel = logLevel.ToNLogLevel();
            if (!this.IsEnabled(nLogLevel))
                return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            string message = this.prefix + formatter(state, exception);

            NLog.LogEventInfo eventInfo = NLog.LogEventInfo.Create(nLogLevel, this.logger.Name, message);
            eventInfo.Exception = exception;
            this.logger.Log(this.wrapperType, eventInfo);
        }
    }
}