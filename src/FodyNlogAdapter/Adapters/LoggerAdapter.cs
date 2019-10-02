using System;
using System.Collections;
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
        private readonly ILogger logger;
        private readonly string typeName;

        private const string TYPE_INFO = "TypeInfo";
        private const string METHOD_INFO = "MethodInfo";
        private readonly string specialPrefix;

        public LoggerAdapter(Type type)
        {
            this.typeName = PrettyFormat(type);
            this.logger = FixLoggerLoggerType(LogManager.GetLogger(type.FullName));
            string configPrefix = Environment.GetEnvironmentVariable("TracerFodySpecialKeyPrefix");
            this.specialPrefix = string.IsNullOrWhiteSpace(configPrefix) ? "$" : configPrefix;
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

        [DebuggerStepThrough]
        public void TraceEnter(string methodInfo, Tuple<string, string>[] methodParameters, string[] paramNames, object[] paramValues)
        {
            if (this.logger.IsTraceEnabled)
            {
                string message;
                var propDict = new Dictionary<string, object>();
                propDict["trace"] = "ENTER";

                if (paramNames != null)
                {
                    var parameters = new StringBuilder();
                    for (int i = 0; i < paramNames.Length; i++)
                    {
                        string rendered = this.RenderVariable(paramValues[i], paramNames[i]);
                        parameters.Append(rendered);

                        if ((i < paramNames.Length - 1) && rendered != string.Empty)
                            parameters.Append(",");
                    }

                    string argInfo = parameters.ToString();
                    propDict["arguments"] = argInfo;
                    message = $"({argInfo})";
                }
                else
                {
                    message = "()";
                }

                this.LogTrace(LogLevel.Trace, methodInfo, message, null, propDict);
            }
        }

        [DebuggerStepThrough]
        public void TraceLeave(string methodInfo, Tuple<string, string>[] methodParameters, long startTicks, long endTicks, string[] paramNames, object[] paramValues)
        {
            if (this.logger.IsTraceEnabled)
            {
                var propDict = new Dictionary<string, object>();
                propDict["trace"] = "LEAVE";

                string returnValue = null;
                if (paramNames != null)
                {
                    var parameters = new StringBuilder();
                    for (int i = 0; i < paramNames.Length; i++)
                    {
                        string rendered = this.RenderVariable(paramValues[i], paramNames[i]);
                        parameters.Append(rendered);

                        if ((i < paramNames.Length - 1) && rendered != string.Empty)
                            parameters.Append(", ");
                    }
                    returnValue = parameters.ToString();
                    propDict["arguments"] = returnValue;
                }

                double timeTaken = ConvertTicksToMilliseconds(endTicks - startTicks);
                propDict["startTicks"] = startTicks;
                propDict["endTicks"] = endTicks;
                propDict["timeTaken"] = timeTaken;

                string message = (returnValue == null) ? "(-)" : $"(-){returnValue}";

                this.LogTrace(LogLevel.Trace, methodInfo, message: $"{message}",
                    exception: null, properties: propDict);
            }
        }

        private string FixSpecialParameterName(string paramName)
        {
            if (paramName[0] == '$')
            {
                return this.specialPrefix + paramName.Substring(1);
            }

            return paramName;
        }

        private static void AddGenericPrettyFormat(StringBuilder sb, Type[] genericArgumentTypes)
        {
            sb.Append("<");
            for (int i = 0; i < genericArgumentTypes.Length; i++)
            {
                sb.Append(genericArgumentTypes[i].Name);
                if (i < genericArgumentTypes.Length - 1) sb.Append(", ");
            }

            sb.Append(">");
        }

        private static double ConvertTicksToMilliseconds(long ticks)
        {
            // ticks * tickFrequency * 10000
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
                sb.Append(type.Name);

            return sb.ToString();
        }

        /// <summary>Renders variable depending on it's type.</summary>
        /// <remarks>Override formats for different messages here.</remarks>
        private string RenderVariable(object variable, string variableName)
        {
            string varName = variableName ?? string.Empty;

            if (variable == null)
                return $"{varName}:{NullString}";

            if (variable is string s)
                return $"{varName}:'{s}'";

            if (variable is IList list)
                return $"{varName}.Count:{list.Count}";

            if (variable is DateTime dateTime)
                return $"{varName}:{dateTime::yyyy-MM-dd HH:mm:ss}";

            if (variable is StringBuilder builder)
                return $"{varName}.Length:{builder.Length}";

            // Other types.
            string stringRepresentation = variable.ToString();

            if (stringRepresentation == variable.GetType().ToString())
            {
                // `.ToString()` is not overloaded and therefore no need to log this variable.
                return string.Empty;
            }

            return $"{varName}:{stringRepresentation}";
        }

        private void LogTrace(LogLevel level, string methodInfo, string message, Exception exception = null, Dictionary<string, object> properties = null)
        {
            var eventData = new LogEventInfo();
            eventData.Exception = exception;
            eventData.Message = message;
            eventData.Level = level;
            eventData.LoggerName = this.logger.Name;

            eventData.Properties.Add(TYPE_INFO, this.typeName);
            eventData.Properties.Add(METHOD_INFO, methodInfo);

            if (properties != null)
            {
                foreach (KeyValuePair<string, object> property in properties)
                    eventData.Properties.Add(property.Key, property.Value);
            }

            this.logger.Log(typeof(LoggerAdapter), eventData);
        }

        public ILogger LogOriginalLogger => this.logger;

        public bool LogIsTraceEnabled => this.logger.IsTraceEnabled;

        public bool LogIsDebugEnabled => this.logger.IsDebugEnabled;

        public bool LogIsInfoEnabled => this.logger.IsInfoEnabled;

        public bool LogIsWarnEnabled => this.logger.IsWarnEnabled;

        public bool LogIsErrorEnabled => this.logger.IsErrorEnabled;

        public bool LogIsFatalEnabled => this.logger.IsFatalEnabled;

        #region Trace() overloads

        public void LogTrace<T>(string methodInfo, T value)
        {
            this.logger.Trace(value);
        }

        public void LogTrace<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            this.logger.Trace(formatProvider, value);
        }

        public void LogTrace(string methodInfo, LogMessageGenerator messageFunc)
        {
            this.logger.Trace(messageFunc);
        }

        public void LogTraceException(string methodInfo, string message, Exception exception)
        {
            this.logger.Trace(message, exception);
        }

        public void LogTrace(string methodInfo, Exception exception, string message)
        {
            this.logger.Trace(exception, message);
        }

        public void LogTrace(string methodInfo, Exception exception, string message, params object[] args)
        {
            this.logger.Trace(exception, message, args);
        }

        public void LogTrace(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            this.logger.Trace(exception, formatProvider, message, args);
        }

        public void LogTrace(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            this.logger.Trace(formatProvider, message, args);
        }

        public void LogTrace(string methodInfo, string message)
        {
            this.logger.Trace(message);
        }

        public void LogTrace(string methodInfo, string message, params object[] args)
        {
            this.logger.Trace(message, args);
        }

        public void LogTrace(string methodInfo, string message, Exception exception)
        {
            this.logger.Trace(message, exception);
        }

        public void LogTrace<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            this.logger.Trace(formatProvider, message, argument);
        }

        public void LogTrace<TArgument>(string methodInfo, string message, TArgument argument)
        {
            this.logger.Trace(message, argument);
        }

        public void LogTrace<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            this.logger.Trace(formatProvider, message, argument1, argument2);
        }

        public void LogTrace<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            this.logger.Trace(message, argument1, argument2);
        }

        public void LogTrace<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Trace(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogTrace<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Trace(message, argument1, argument2, argument3);
        }


        #endregion

        #region Debug() overloads

        public void LogDebug<T>(string methodInfo, T value)
        {
            this.logger.Debug(value);
        }

        public void LogDebug<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            this.logger.Debug(formatProvider, value);
        }

        public void LogDebug(string methodInfo, LogMessageGenerator messageFunc)
        {
            this.logger.Debug(messageFunc);
        }

        public void LogDebugException(string methodInfo, string message, Exception exception)
        {
            this.logger.Debug(message, exception);
        }

        public void LogDebug(string methodInfo, Exception exception, string message)
        {
            this.logger.Debug(exception, message);
        }

        public void LogDebug(string methodInfo, Exception exception, string message, params object[] args)
        {
            this.logger.Debug(exception, message, args);
        }

        public void LogDebug(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            this.logger.Debug(exception, formatProvider, message, args);
        }

        public void LogDebug(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            this.logger.Debug(formatProvider, message, args);
        }

        public void LogDebug(string methodInfo, string message)
        {
            this.logger.Debug(message);
        }

        public void LogDebug(string methodInfo, string message, params object[] args)
        {
            this.logger.Debug(message, args);
        }

        public void LogDebug(string methodInfo, string message, Exception exception)
        {
            this.logger.Debug(message, exception);
        }

        public void LogDebug<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            this.logger.Debug(formatProvider, message, argument);
        }

        public void LogDebug<TArgument>(string methodInfo, string message, TArgument argument)
        {
            this.logger.Debug(message, argument);
        }

        public void LogDebug<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            this.logger.Debug(formatProvider, message, argument1, argument2);
        }

        public void LogDebug<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            this.logger.Debug(message, argument1, argument2);
        }

        public void LogDebug<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Debug(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogDebug<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Debug(message, argument1, argument2, argument3);
        }


        #endregion

        #region Info() overloads

        public void LogInfo<T>(string methodInfo, T value)
        {
            this.logger.Info(value);
        }

        public void LogInfo<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            this.logger.Info(formatProvider, value);
        }

        public void LogInfo(string methodInfo, LogMessageGenerator messageFunc)
        {
            this.logger.Info(messageFunc);
        }

        public void LogInfoException(string methodInfo, string message, Exception exception)
        {
            this.logger.Info(message, exception);
        }

        public void LogInfo(string methodInfo, Exception exception, string message)
        {
            this.logger.Info(exception, message);
        }

        public void LogInfo(string methodInfo, Exception exception, string message, params object[] args)
        {
            this.logger.Info(exception, message, args);
        }

        public void LogInfo(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            this.logger.Info(exception, formatProvider, message, args);
        }

        public void LogInfo(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            this.logger.Info(formatProvider, message, args);
        }

        public void LogInfo(string methodInfo, string message)
        {
            this.logger.Info(message);
        }

        public void LogInfo(string methodInfo, string message, params object[] args)
        {
            this.logger.Info(message, args);
        }

        public void LogInfo(string methodInfo, string message, Exception exception)
        {
            this.logger.Info(message, exception);
        }

        public void LogInfo<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            this.logger.Info(formatProvider, message, argument);
        }

        public void LogInfo<TArgument>(string methodInfo, string message, TArgument argument)
        {
            this.logger.Info(message, argument);
        }

        public void LogInfo<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            this.logger.Info(formatProvider, message, argument1, argument2);
        }

        public void LogInfo<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            this.logger.Info(message, argument1, argument2);
        }

        public void LogInfo<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Info(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogInfo<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Info(message, argument1, argument2, argument3);
        }


        #endregion

        #region Warn() overloads

        public void LogWarn<T>(string methodInfo, T value)
        {
            this.logger.Warn(value);
        }

        public void LogWarn<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            this.logger.Warn(formatProvider, value);
        }

        public void LogWarn(string methodInfo, LogMessageGenerator messageFunc)
        {
            this.logger.Warn(messageFunc);
        }

        public void LogWarnException(string methodInfo, string message, Exception exception)
        {
            this.logger.Warn(message, exception);
        }

        public void LogWarn(string methodInfo, Exception exception, string message)
        {
            this.logger.Warn(exception, message);
        }

        public void LogWarn(string methodInfo, Exception exception, string message, params object[] args)
        {
            this.logger.Warn(exception, message, args);
        }

        public void LogWarn(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            this.logger.Warn(exception, formatProvider, message, args);
        }

        public void LogWarn(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            this.logger.Warn(formatProvider, message, args);
        }

        public void LogWarn(string methodInfo, string message)
        {
            this.logger.Warn(message);
        }

        public void LogWarn(string methodInfo, string message, params object[] args)
        {
            this.logger.Warn(message, args);
        }

        public void LogWarn(string methodInfo, string message, Exception exception)
        {
            this.logger.Warn(message, exception);
        }

        public void LogWarn<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            this.logger.Warn(formatProvider, message, argument);
        }

        public void LogWarn<TArgument>(string methodInfo, string message, TArgument argument)
        {
            this.logger.Warn(message, argument);
        }

        public void LogWarn<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            this.logger.Warn(formatProvider, message, argument1, argument2);
        }

        public void LogWarn<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            this.logger.Warn(message, argument1, argument2);
        }

        public void LogWarn<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Warn(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogWarn<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Warn(message, argument1, argument2, argument3);
        }


        #endregion

        #region Error() overloads

        public void LogError<T>(string methodInfo, T value)
        {
            this.logger.Error(value);
        }

        public void LogError<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            this.logger.Error(formatProvider, value);
        }

        public void LogError(string methodInfo, LogMessageGenerator messageFunc)
        {
            this.logger.Error(messageFunc);
        }

        public void LogErrorException(string methodInfo, string message, Exception exception)
        {
            this.logger.Error(message, exception);
        }

        public void LogError(string methodInfo, Exception exception, string message)
        {
            this.logger.Error(exception, message);
        }

        public void LogError(string methodInfo, Exception exception, string message, params object[] args)
        {
            this.logger.Error(exception, message, args);
        }

        public void LogError(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            this.logger.Error(exception, formatProvider, message, args);
        }

        public void LogError(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            this.logger.Error(formatProvider, message, args);
        }

        public void LogError(string methodInfo, string message)
        {
            this.logger.Error(message);
        }

        public void LogError(string methodInfo, string message, params object[] args)
        {
            this.logger.Error(message, args);
        }

        public void LogError(string methodInfo, string message, Exception exception)
        {
            this.logger.Error(message, exception);
        }

        public void LogError<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            this.logger.Error(formatProvider, message, argument);
        }

        public void LogError<TArgument>(string methodInfo, string message, TArgument argument)
        {
            this.logger.Error(message, argument);
        }

        public void LogError<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            this.logger.Error(formatProvider, message, argument1, argument2);
        }

        public void LogError<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            this.logger.Error(message, argument1, argument2);
        }

        public void LogError<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Error(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogError<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Error(message, argument1, argument2, argument3);
        }


        #endregion

        #region Fatal() overloads

        public void LogFatal<T>(string methodInfo, T value)
        {
            this.logger.Fatal(value);
        }

        public void LogFatal<T>(string methodInfo, IFormatProvider formatProvider, T value)
        {
            this.logger.Fatal(formatProvider, value);
        }

        public void LogFatal(string methodInfo, LogMessageGenerator messageFunc)
        {
            this.logger.Fatal(messageFunc);
        }

        public void LogFatalException(string methodInfo, string message, Exception exception)
        {
            this.logger.Fatal(message, exception);
        }

        public void LogFatal(string methodInfo, Exception exception, string message)
        {
            this.logger.Fatal(exception, message);
        }

        public void LogFatal(string methodInfo, Exception exception, string message, params object[] args)
        {
            this.logger.Fatal(exception, message, args);
        }

        public void LogFatal(string methodInfo, Exception exception, IFormatProvider formatProvider, string message,
            params object[] args)
        {
            this.logger.Fatal(exception, formatProvider, message, args);
        }

        public void LogFatal(string methodInfo, IFormatProvider formatProvider, string message, params object[] args)
        {
            this.logger.Fatal(formatProvider, message, args);
        }

        public void LogFatal(string methodInfo, string message)
        {
            this.logger.Fatal(message);
        }

        public void LogFatal(string methodInfo, string message, params object[] args)
        {
            this.logger.Fatal(message, args);
        }

        public void LogFatal(string methodInfo, string message, Exception exception)
        {
            this.logger.Fatal(message, exception);
        }

        public void LogFatal<TArgument>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument argument)
        {
            this.logger.Fatal(formatProvider, message, argument);
        }

        public void LogFatal<TArgument>(string methodInfo, string message, TArgument argument)
        {
            this.logger.Fatal(message, argument);
        }

        public void LogFatal<TArgument1, TArgument2>(string methodInfo, IFormatProvider formatProvider, string message,
            TArgument1 argument1, TArgument2 argument2)
        {
            this.logger.Fatal(formatProvider, message, argument1, argument2);
        }

        public void LogFatal<TArgument1, TArgument2>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2)
        {
            this.logger.Fatal(message, argument1, argument2);
        }

        public void LogFatal<TArgument1, TArgument2, TArgument3>(string methodInfo, IFormatProvider formatProvider,
            string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Fatal(formatProvider, message, argument1, argument2, argument3);
        }

        public void LogFatal<TArgument1, TArgument2, TArgument3>(string methodInfo, string message, TArgument1 argument1,
            TArgument2 argument2, TArgument3 argument3)
        {
            this.logger.Fatal(message, argument1, argument2, argument3);
        }


        #endregion
    }
}
