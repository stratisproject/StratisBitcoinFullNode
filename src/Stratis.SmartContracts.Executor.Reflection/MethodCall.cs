using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents a call to a contract method.
    /// </summary>
    public class MethodCall
    {
        private readonly string methodName;

        public const string FallbackMethodName = nameof(SmartContract.Fallback);

        public const string ExternalFallbackMethodName = "";

        public MethodCall(string method, object[] methodParameters = null)
        {
            this.methodName = method;
            this.Parameters = methodParameters;
        }

        public static MethodCall Fallback()
        {
            return new MethodCall(ExternalFallbackMethodName);
        }

        public object[] Parameters { get; }

        public string Name
        {
            get
            {
                return this.IsFallbackCall
                    ? FallbackMethodName
                    : this.methodName;
            }
        }

        /// <summary>
        /// Returns true if the method call is a fallback method call.
        /// </summary>
        public bool IsFallbackCall
        {
            get
            {
                // The fallback method must always be the override with no parameters,
                // so it's not enough just to check if the method name is correct.
                return (this.Parameters == null || this.Parameters.Length == 0) 
                       && this.methodName != null
                       && (this.methodName.Equals(FallbackMethodName) 
                           || ExternalFallbackMethodName.Equals(this.methodName, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}