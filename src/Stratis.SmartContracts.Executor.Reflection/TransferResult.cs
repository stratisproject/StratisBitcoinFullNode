﻿using System;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Returned when making a transfer from a smart contract.
    /// </summary>
    public sealed class TransferResult : ITransferResult
    {
        /// <summary>The return value of the method called.</summary>
        public object ReturnValue { get; private set; }

        /// <summary>Whether execution of the contract method was successful.</summary>
        public bool Success { get; }

        private TransferResult(bool success)
        {
            this.Success = success;
        }

        /// <summary>
        /// Constructs a an empty result i.e. a transfer from a contract was not defined/executed.
        /// </summary>
        public static TransferResult Empty()
        {
            return new TransferResult(true);
        }

        /// <summary>
        /// Constructs a result when a transfer from a contract failed.
        /// </summary>
        public static TransferResult Failed()
        {
            return new TransferResult(false);
        }

        /// <summary>
        /// Constructs a result when a transfer from a contract succeeded.
        /// </summary>
        /// <param name="returnValue">The object that was returned from the executor.</param>
        public static TransferResult Transferred(object returnValue)
        {
            var result = new TransferResult(true)
            {
                ReturnValue = returnValue
            };

            return result;
        }
    }
}