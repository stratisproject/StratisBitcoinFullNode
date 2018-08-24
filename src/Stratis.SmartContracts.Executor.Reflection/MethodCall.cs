using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents a call to a contract method.
    /// </summary>
    public class MethodCall
    {
        private readonly string methodName;

        public const string ReceiveHandlerName = nameof(SmartContract.Receive);

        public const string ExternalReceiveHandlerName = "";

        public MethodCall(string methodName, object[] methodParameters = null)
        {
            this.methodName = methodName;
            this.Parameters = methodParameters;
        }

        public static MethodCall Receive()
        {
            return new MethodCall(ExternalReceiveHandlerName);
        }

        public object[] Parameters { get; }

        public string Name
        {
            get
            {
                return this.IsReceiveHandlerCall
                    ? ReceiveHandlerName
                    : this.methodName;
            }
        }

        /// <summary>
        /// Returns true if the method call is a receive handler call.
        /// </summary>
        public bool IsReceiveHandlerCall
        {
            get
            {
                // The receive handler must always be the override with no parameters,
                // so it's not enough just to check if the method name is correct.
                return (this.Parameters == null || this.Parameters.Length == 0) 
                       && this.methodName != null
                       && (ReceiveHandlerName.Equals(this.methodName, StringComparison.OrdinalIgnoreCase) 
                           || ExternalReceiveHandlerName.Equals(this.methodName, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}