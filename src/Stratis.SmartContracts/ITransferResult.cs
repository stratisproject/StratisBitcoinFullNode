using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Defines what gets returned should a contract execute a transfer.
    /// </summary>
    public interface ITransferResult
    {
        /// <summary>
        /// The return value of the method called.
        /// </summary>
        object ReturnValue { get; }

        /// <summary>
        /// If there was an error during execution of the selected method it will be stored here.
        /// </summary>
        Exception ThrownException { get; }

        /// <summary>
        /// Whether execution of the contract method was successful.
        /// </summary>
        bool Success { get; }
    }
}