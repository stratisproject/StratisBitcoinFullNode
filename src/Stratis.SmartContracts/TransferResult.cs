using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Returned when making a transfer from a smart contract.
    /// </summary>
    public class TransferResult
    {
        /// <summary>
        /// The return value of the method called.
        /// </summary>
        public object ReturnValue { get; private set; }

        /// <summary>
        /// If there was an error during execution of the selected method it will be stored here.
        /// </summary>
        public Exception ThrownException { get; private set; }

        /// <summary>
        /// Whether execution of the contract method was successful.
        /// </summary>
        public bool Success
        {
            get
            {
                return this.ThrownException == null;
            }
        }

        internal TransferResult() { }

        internal TransferResult(object returnValue, Exception thrownException)
        {
            this.ReturnValue = returnValue;
            this.ThrownException = thrownException;
        }
    }
}
