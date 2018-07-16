using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Returned when making a transfer from a smart contract.
    /// </summary>
    public sealed class TransferResult : ITransferResult
    {
        /// <summary>The return value of the method called.</summary>
        public object ReturnValue { get; private set; }

        /// <summary>
        /// If there was an error during execution of the selected method it will be stored here.
        /// </summary>
        public Exception ThrownException { get; private set; }

        /// <summary>Whether execution of the contract method was successful.</summary>
        public bool Success
        {
            get { return this.ThrownException == null; }
        }

        private TransferResult() { }

        /// <summary>
        /// Constructs a an empty result i.e. a transfer from a contract was not defined/executed.
        /// </summary>
        public static TransferResult Empty()
        {
            return new TransferResult();
        }

        /// <summary>
        /// Constructs a result when a transfer from a contract failed.
        /// </summary>
        /// <param name="thrownException">The exception that was thrown by the executor.</param>
        public static TransferResult Failed(Exception thrownException)
        {
            var result = new TransferResult
            {
                ThrownException = thrownException
            };
            return result;
        }

        /// <summary>
        /// Constructs a result when a transfer from a contract succeeded.
        /// </summary>
        /// <param name="returnValue">The object that was returned from the executor.</param>
        public static TransferResult Transferred(object returnValue)
        {
            var result = new TransferResult
            {
                ReturnValue = returnValue
            };
            return result;
        }
    }
}