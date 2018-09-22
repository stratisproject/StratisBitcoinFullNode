using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NLog;

namespace FodyNlogAdapter.Adapters
{
    public class LoggerAdapter
    {
        private const string NullString = "<NULL>";
        private readonly ILogger _logger;
        private readonly string _typeName;

        private const string TYPE_INFO = "TypeInfo";
        private const string METHOD_INFO = "MethodInfo";
        private readonly string _specialPrefix;

        public LoggerAdapter(Type type)
        {
            _typeName = PrettyFormat(type);
            _logger = FixLoggerLoggerType(LogManager.GetLogger(type.FullName));
            var configPrefix = Environment.GetEnvironmentVariable("TracerFodySpecialKeyPrefix");
            _specialPrefix = string.IsNullOrWhiteSpace(configPrefix) ? "$" : configPrefix;
        }

        private static readonly FieldInfo LoggerTypeField = typeof(Logger).GetField("loggerType", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Logger FixLoggerLoggerType(Logger logger)
        {
            if (LoggerTypeField != null)
            {
                LoggerTypeField.SetValue(logger, typeof(LoggerAdapter));
            }
            return logger;
        }

        #region Methods required for trace enter and leave

        public void TraceEnter(string methodInfo, string[] paramNames, object[] paramValues)
        {
            if (_logger.IsTraceEnabled)
            {
                string message;
                var propDict = new Dictionary<string, object>();
                propDict["trace"] = "ENTER";

                if (paramNames != null)
                {
                    var parameters = new StringBuilder();
                    for (int i = 0; i < paramNames.Length; i++)
                    {
                        parameters.AppendFormat("{0}={1}", paramNames[i], GetRenderedFormat(paramValues[i], NullString));
                        if (i < paramNames.Length - 1) parameters.Append(", ");
                    }
                    var argInfo = parameters.ToString();
                    propDict["arguments"] = argInfo;
                    message = String.Format("Entered into {0} ({1}).", methodInfo, argInfo);
                }
                else
                {
                    message = String.Format("Entered into {0}.", methodInfo);
                }
                LogTrace(LogLevel.Trace, methodInfo, message, null, propDict);
            }
        }

        public void TraceLeave(string methodInfo, long startTicks, long endTicks, string[] paramNames, object[] paramValues)
        {
            if (_logger.IsTraceEnabled)
            {
                var propDict = new Dictionary<string, object>();
                propDict["trace"] = "LEAVE";

                string returnValue = null;
                if (paramNames != null)
                {
                    var parameters = new StringBuilder();
                    for (int i = 0; i < paramNames.Length; i++)
                    {
                        parameters.AppendFormat("{0}={1}", FixSpecialParameterName(paramNames[i] ?? "$return"), GetRenderedFormat(paramValues[i], NullString));
                        if (i < paramNames.Length - 1) parameters.Append(", ");
                    }
                    returnValue = parameters.ToString();
                    propDict["arguments"] = returnValue;
                }

                var timeTaken = ConvertTicksToMilliseconds(endTicks - startTicks);
                propDict["startTicks"] = startTicks;
                propDict["endTicks"] = endTicks;
                propDict["timeTaken"] = timeTaken;

                LogTrace(LogLevel.Trace, methodInfo,
                    String.Format("Returned from {1} ({2}). Time taken: {0:0.00} ms.",
                        timeTaken, methodInfo, returnValue), null, propDict);
            }
        }

        private string FixSpecialParameterName(string paramName)
        {
            if (paramName[0] == '$')
            {
                return _specialPrefix + paramName.Substring(1);
            }

            return paramName;
        }

        private static void AddGenericPrettyFormat(StringBuilder sb, Type[] genericArgumentTypes)
        {
            sb.Append("<");
            for (var i = 0; i < genericArgumentTypes.Length; i++)
            {
                sb.Append(genericArgumentTypes[i].Name);
                if (i < genericArgumentTypes.Length - 1) sb.Append(", ");
            }
            sb.Append(">");
        }

        private static double ConvertTicksToMilliseconds(long ticks)
        {
            //ticks * tickFrequency * 10000
            return ticks * (10000000 / (double)Stopwatch.Frequency) / 10000L;
        }

        private static string PrettyFormat(Type type)
        {
            var sb = new StringBuilder();
            if (type.IsGenericType)
            {
                sb.Append(type.Name.Remove(type.Name.IndexOf('`')));
                AddGenericPrettyFormat(sb, type.GetGenericArguments());
            }
            else
            {
                sb.Append(type.Name);
            }
            return sb.ToString();
        }

        private string GetRenderedFormat(object message, string stringRepresentationOfNull = "")
        {
            if (message == null)
                return stringRepresentationOfNull;
            if (message is string)
                return (string)message;
            return message.ToString();
        }

        private void LogTrace(LogLevel level, string methodInfo, string message, Exception exception = null, Dictionary<string, object> properties = null)
        {
            var eventData = new LogEventInfo();
            eventData.Exception = exception;
            eventData.Message = message;
            eventData.Level = level;
            eventData.LoggerName = _logger.Name;

            eventData.Properties.Add(TYPE_INFO, _typeName);
            eventData.Properties.Add(METHOD_INFO, methodInfo);

            if (properties != null)
            {
                foreach (var property in properties)
                    eventData.Properties.Add(property.Key, property.Value);
            }

            _logger.Log(typeof(LoggerAdapter), eventData);
        }

        #endregion

        public ILogger LogOriginalLogger
        {
            get { return _logger; }
        }

        public bool LogIsTraceEnabled
        {
            get { return _logger.IsTraceEnabled; }
        }


        public bool LogIsDebugEnabled
        {
            get { return _logger.IsDebugEnabled; }
        }

        public bool LogIsInfoEnabled
        {
            get { return _logger.IsInfoEnabled; }
        }

        public bool LogIsWarnEnabled
        {
            get { return _logger.IsWarnEnabled; }
        }

        public bool LogIsErrorEnabled
        {
            get { return _logger.IsErrorEnabled; }
        }

        public bool LogIsFatalEnabled
        {
            get { return _logger.IsFatalEnabled; }
        }

        #region Trace() overloads

        public void LogTrace<T>(string methodInfo, T value)
        {
            _logger.Trace(value);
        }

        public void LogTrace<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            _logger.Trace(formatProvider, value);
        }

        public void LogTrace(string methodInfo, LogMessageGenerator messageFunc)
        {
            _logger.Trace(messageFunc);
        }

        public void LogTraceException(string methodInfo, string message, Exception exception)
        {
            _logger.Trace(message, exception);
        }

        public void LogTrace(string methodInfo, Exception exception, string message)
        {
            _logger.Trace(exception, message);
        }

        public void LogTrace(string methodInfo, Exception exception, string message, params object[] args)
        {
            _logger.Trace(exception, message, args);
        }

        public void LogTrace(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            _logger.Trace(exception, formatProvider, message, args);
        }

        public void LogTrace(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            _logger.Trace(formatProvider, message, args);
        }

        public void LogTrace(string methodInfo, string message)
        {
            _logger.Trace(message);
        }

        public void LogTrace(string methodInfo, string message, params object[] args)
        {
            _logger.Trace(message, args);
        }

        public void LogTrace(string methodInfo, string message, Exception exception)
        {
            _logger.Trace(message, exception);
        }

        public void LogTrace<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            _logger.Trace(formatProvider, message, argument);
        }

        public void LogTrace<TArgument>(string methodInfo, string message, TArgument argument)
        {
            _logger.Trace(message, argument);
        }

        public void LogTrace<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            _logger.Trace(formatProvider, message, argument1, argument2);
        }

        public void LogTrace<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            _logger.Trace(message, argument1, argument2);
        }

        public void LogTrace<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Trace(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogTrace<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Trace(message, argument1, argument2, argument3);
        }


        #endregion

        #region Debug() overloads

        public void LogDebug<T>(string methodInfo, T value)
        {
            _logger.Debug(value);
        }

        public void LogDebug<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            _logger.Debug(formatProvider, value);
        }

        public void LogDebug(string methodInfo, LogMessageGenerator messageFunc)
        {
            _logger.Debug(messageFunc);
        }

        public void LogDebugException(string methodInfo, string message, Exception exception)
        {
            _logger.Debug(message, exception);
        }

        public void LogDebug(string methodInfo, Exception exception, string message)
        {
            _logger.Debug(exception, message);
        }

        public void LogDebug(string methodInfo, Exception exception, string message, params object[] args)
        {
            _logger.Debug(exception, message, args);
        }

        public void LogDebug(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            _logger.Debug(exception, formatProvider, message, args);
        }

        public void LogDebug(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            _logger.Debug(formatProvider, message, args);
        }

        public void LogDebug(string methodInfo, string message)
        {
            _logger.Debug(message);
        }

        public void LogDebug(string methodInfo, string message, params object[] args)
        {
            _logger.Debug(message, args);
        }

        public void LogDebug(string methodInfo, string message, Exception exception)
        {
            _logger.Debug(message, exception);
        }

        public void LogDebug<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            _logger.Debug(formatProvider, message, argument);
        }

        public void LogDebug<TArgument>(string methodInfo, string message, TArgument argument)
        {
            _logger.Debug(message, argument);
        }

        public void LogDebug<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            _logger.Debug(formatProvider, message, argument1, argument2);
        }

        public void LogDebug<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            _logger.Debug(message, argument1, argument2);
        }

        public void LogDebug<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Debug(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogDebug<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Debug(message, argument1, argument2, argument3);
        }


        #endregion

        #region Info() overloads

        public void LogInfo<T>(string methodInfo, T value)
        {
            _logger.Info(value);
        }

        public void LogInfo<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            _logger.Info(formatProvider, value);
        }

        public void LogInfo(string methodInfo, LogMessageGenerator messageFunc)
        {
            _logger.Info(messageFunc);
        }

        public void LogInfoException(string methodInfo, string message, Exception exception)
        {
            _logger.Info(message, exception);
        }

        public void LogInfo(string methodInfo, Exception exception, string message)
        {
            _logger.Info(exception, message);
        }

        public void LogInfo(string methodInfo, Exception exception, string message, params object[] args)
        {
            _logger.Info(exception, message, args);
        }

        public void LogInfo(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            _logger.Info(exception, formatProvider, message, args);
        }

        public void LogInfo(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            _logger.Info(formatProvider, message, args);
        }

        public void LogInfo(string methodInfo, string message)
        {
            _logger.Info(message);
        }

        public void LogInfo(string methodInfo, string message, params object[] args)
        {
            _logger.Info(message, args);
        }

        public void LogInfo(string methodInfo, string message, Exception exception)
        {
            _logger.Info(message, exception);
        }

        public void LogInfo<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            _logger.Info(formatProvider, message, argument);
        }

        public void LogInfo<TArgument>(string methodInfo, string message, TArgument argument)
        {
            _logger.Info(message, argument);
        }

        public void LogInfo<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            _logger.Info(formatProvider, message, argument1, argument2);
        }

        public void LogInfo<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            _logger.Info(message, argument1, argument2);
        }

        public void LogInfo<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Info(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogInfo<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Info(message, argument1, argument2, argument3);
        }


        #endregion

        #region Warn() overloads

        public void LogWarn<T>(string methodInfo, T value)
        {
            _logger.Warn(value);
        }

        public void LogWarn<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            _logger.Warn(formatProvider, value);
        }

        public void LogWarn(string methodInfo, LogMessageGenerator messageFunc)
        {
            _logger.Warn(messageFunc);
        }

        public void LogWarnException(string methodInfo, string message, Exception exception)
        {
            _logger.Warn(message, exception);
        }

        public void LogWarn(string methodInfo, Exception exception, string message)
        {
            _logger.Warn(exception, message);
        }

        public void LogWarn(string methodInfo, Exception exception, string message, params object[] args)
        {
            _logger.Warn(exception, message, args);
        }

        public void LogWarn(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            _logger.Warn(exception, formatProvider, message, args);
        }

        public void LogWarn(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            _logger.Warn(formatProvider, message, args);
        }

        public void LogWarn(string methodInfo, string message)
        {
            _logger.Warn(message);
        }

        public void LogWarn(string methodInfo, string message, params object[] args)
        {
            _logger.Warn(message, args);
        }

        public void LogWarn(string methodInfo, string message, Exception exception)
        {
            _logger.Warn(message, exception);
        }

        public void LogWarn<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            _logger.Warn(formatProvider, message, argument);
        }

        public void LogWarn<TArgument>(string methodInfo, string message, TArgument argument)
        {
            _logger.Warn(message, argument);
        }

        public void LogWarn<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            _logger.Warn(formatProvider, message, argument1, argument2);
        }

        public void LogWarn<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            _logger.Warn(message, argument1, argument2);
        }

        public void LogWarn<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Warn(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogWarn<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Warn(message, argument1, argument2, argument3);
        }


        #endregion

        #region Error() overloads

        public void LogError<T>(string methodInfo, T value)
        {
            _logger.Error(value);
        }

        public void LogError<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            _logger.Error(formatProvider, value);
        }

        public void LogError(string methodInfo, LogMessageGenerator messageFunc)
        {
            _logger.Error(messageFunc);
        }

        public void LogErrorException(string methodInfo, string message, Exception exception)
        {
            _logger.Error(message, exception);
        }

        public void LogError(string methodInfo, Exception exception, string message)
        {
            _logger.Error(exception, message);
        }

        public void LogError(string methodInfo, Exception exception, string message, params object[] args)
        {
            _logger.Error(exception, message, args);
        }

        public void LogError(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            _logger.Error(exception, formatProvider, message, args);
        }

        public void LogError(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            _logger.Error(formatProvider, message, args);
        }

        public void LogError(string methodInfo, string message)
        {
            _logger.Error(message);
        }

        public void LogError(string methodInfo, string message, params object[] args)
        {
            _logger.Error(message, args);
        }

        public void LogError(string methodInfo, string message, Exception exception)
        {
            _logger.Error(message, exception);
        }

        public void LogError<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            _logger.Error(formatProvider, message, argument);
        }

        public void LogError<TArgument>(string methodInfo, string message, TArgument argument)
        {
            _logger.Error(message, argument);
        }

        public void LogError<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            _logger.Error(formatProvider, message, argument1, argument2);
        }

        public void LogError<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            _logger.Error(message, argument1, argument2);
        }

        public void LogError<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Error(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogError<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Error(message, argument1, argument2, argument3);
        }


        #endregion

        #region Fatal() overloads

        public void LogFatal<T>(string methodInfo, T value)
        {
            _logger.Fatal(value);
        }

        public void LogFatal<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            _logger.Fatal(formatProvider, value);
        }

        public void LogFatal(string methodInfo, LogMessageGenerator messageFunc)
        {
            _logger.Fatal(messageFunc);
        }

        public void LogFatalException(string methodInfo, string message, Exception exception)
        {
            _logger.Fatal(message, exception);
        }

        public void LogFatal(string methodInfo, Exception exception, string message)
        {
            _logger.Fatal(exception, message);
        }

        public void LogFatal(string methodInfo, Exception exception, string message, params object[] args)
        {
            _logger.Fatal(exception, message, args);
        }

        public void LogFatal(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            _logger.Fatal(exception, formatProvider, message, args);
        }

        public void LogFatal(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            _logger.Fatal(formatProvider, message, args);
        }

        public void LogFatal(string methodInfo, string message)
        {
            _logger.Fatal(message);
        }

        public void LogFatal(string methodInfo, string message, params object[] args)
        {
            _logger.Fatal(message, args);
        }

        public void LogFatal(string methodInfo, string message, Exception exception)
        {
            _logger.Fatal(message, exception);
        }

        public void LogFatal<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            _logger.Fatal(formatProvider, message, argument);
        }

        public void LogFatal<TArgument>(string methodInfo, string message, TArgument argument)
        {
            _logger.Fatal(message, argument);
        }

        public void LogFatal<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            _logger.Fatal(formatProvider, message, argument1, argument2);
        }

        public void LogFatal<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            _logger.Fatal(message, argument1, argument2);
        }

        public void LogFatal<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Fatal(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogFatal<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            _logger.Fatal(message, argument1, argument2, argument3);
        }


        #endregion
    }
}
