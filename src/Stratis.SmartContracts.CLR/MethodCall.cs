using System;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Represents a call to a contract method.
    /// </summary>
    public class MethodCall
    {
        private readonly string methodName;

        /// <summary>
        /// The name of the receive handler method on the contract object.
        /// </summary>
        public const string ReceiveHandlerName = nameof(SmartContract.Receive);

        /// <summary>
        /// Alias for the receive handler method name. Will cause the receive handler to be invoked
        /// if specified as the method name in a transaction.
        /// </summary>
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