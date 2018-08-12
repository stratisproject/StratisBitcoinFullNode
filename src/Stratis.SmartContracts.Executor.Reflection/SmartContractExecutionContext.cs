﻿using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class TransactionContext : ITransactionContext
    {
        private ulong nonce = 0;

        public TransactionContext(uint256 txHash, ulong blockHeight, uint160 coinbase, uint160 sender, ulong amount, ulong nonce = 0)
        {
            this.TransactionHash = txHash;
            this.BlockHeight = blockHeight;
            this.Coinbase = coinbase;
            this.From = sender;
            this.Amount = amount;
            this.nonce = nonce;
        }

        public Money Amount { get; }
        public uint256 TransactionHash { get; }
        public uint160 Coinbase { get; }
        public ulong BlockHeight { get; }
        public uint160 From { get; }
        public uint160 To { get; }

        public ulong GetNonceAndIncrement()
        {
            return this.nonce++;
        }
    }

    public interface ITransactionContext
    {
        /// <summary>
        /// The amount sent with the transaction
        /// </summary>
        Money Amount { get; }

        /// <summary>
        /// Hash of the currently executing transaction.
        /// </summary>
        uint256 TransactionHash { get; }

        /// <summary>
        /// Address of the coinbase for the current block.
        /// </summary>
        uint160 Coinbase { get; }

        /// <summary>
        /// Height of the current block in the chain.
        /// </summary>
        ulong BlockHeight { get; }

        /// <summary>
        /// Address of the sender for the current contract call.
        /// </summary>
        uint160 From { get; }

        /// <summary>
        /// The destination address
        /// </summary>
        uint160 To { get; }

        /// <summary>
        /// Get the next number to use as the nonce in the contract address calculation and then 
        /// increment the number behind the scenes, so that the next contract address calculation
        /// gets a new number. 
        /// </summary>
        /// <returns>The next nonce to use in the contract address formula.</returns>
        ulong GetNonceAndIncrement();
    }

    /// <summary>
    /// Information about the current state of the blockchain that is passed into the virtual machine.
    /// </summary>
    public sealed class SmartContractExecutionContext : ISmartContractExecutionContext
    {
        /// <inheritdoc/>
        public IBlock Block { get; }

        /// <inheritdoc/>
        public uint160 ContractAddress { get; set; }

        /// <inheritdoc/>
        public ulong GasPrice { get; }

        /// <inheritdoc/>
        public IMessage Message { get; }

        /// <inheritdoc/>
        public object[] Parameters { get; private set; }

        public SmartContractExecutionContext(IBlock block, IMessage message, uint160 contractAdddress, ulong gasPrice, object[] methodParameters = null)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(message, nameof(message));

            this.Block = block;
            this.Message = message;
            this.GasPrice = gasPrice;
            this.ContractAddress = contractAdddress;

            if (methodParameters != null && methodParameters.Length > 0)
                this.Parameters = methodParameters;
        }
    }
}