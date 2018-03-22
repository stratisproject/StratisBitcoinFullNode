using System;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Backend
{
    public interface ISmartContractExecutionResult
    {
        /// <summary>
        /// The amount of gas units used through execution of the smart contract.
        /// </summary>
        Gas GasUnitsUsed { get; set; }

        /// <summary>
        /// If an object is returned from the method called, it will be stored here.
        /// </summary>
        object Return { get; set; }

        /// <summary>
        /// If there is an exception during execution, it will be stored here.
        /// </summary>
        Exception Exception { get; set; }

        /// <summary>
        /// Whether the state changes made during execution should be reverted. If an exception occurred, then should be true.
        /// </summary>
        bool Revert { get; }

        /// <summary>
        /// The condensing transaction produced by the contract execution.
        /// </summary>
        Transaction InternalTransaction { get; set; }

        /// <summary>
        /// Used in Ethereum to increase a gas refund.
        /// </summary>
        ulong FutureRefund { get; set; }

        /// <summary>
        /// If the execution created a new contract, its address will be stored here.
        /// </summary>
        uint160 NewContractAddress { get; set; }

        /// <summary>
        /// After a contract is executed internally, we will need to merge the results.
        /// </summary>
        /// <param name="another"></param>
        void Merge(ISmartContractExecutionResult another);
    }
}