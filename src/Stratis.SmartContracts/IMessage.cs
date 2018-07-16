﻿namespace Stratis.SmartContracts
{
    public interface IMessage
    {
        /// <summary>
        /// The address of the contract currently being executed.
        /// </summary>
        Address ContractAddress { get; }

        /// <summary>
        /// The address that called this contract.
        /// </summary>
        Address Sender { get; }

        /// <summary>
        /// The total gas allocated allowed to be spend during contract execution.
        /// </summary>
        Gas GasLimit { get; }

        /// <summary>
        /// The amount of STRAT sent in this call. 
        /// </summary>
        ulong Value { get; }
    }
}