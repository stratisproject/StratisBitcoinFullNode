using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.Test
{
    public interface ITestChain
    {
        /// <summary>
        /// 10 addresses that come preloaded with funds.
        /// </summary>
        IReadOnlyList<string> PreloadedAddresses { get; }

        /// <summary>
        /// Get the balance in stratoshis for an address.
        /// </summary>
        /// <param name="address">Address to get the balance for.</param>
        /// <returns>Balance for this address.</returns>
        ulong GetBalanceInStratoshis(string address);

        /// <summary>
        /// Performs all required setup for network.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Mine blocks on the network. Miner selection algorithm depends on consensus model.
        /// </summary>
        /// <param name="num">Number of blocks to mine.</param>
        void MineBlocks(int num);

        BuildCreateContractTransactionResponse SendCreateContractTransaction(
            string from,
            byte[] contractCode,
            double amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatRule.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            double feeAmount = 0.01);

        BuildCallContractTransactionResponse SendCallContractTransaction(
            string from,
            string methodName,
            string contractAddress,
            double amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatRule.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            double feeAmount = 0.01);

        /// <summary>
        /// Get the bytecode stored at a particular contract address.
        /// </summary>
        /// <param name="contractAddress">Address of the contract to get code for.</param>
        /// <returns>Code at this address.</returns>
        byte[] GetCode(string contractAddress);

        /// <summary>
        /// Get the receipt details for a transaction.
        /// </summary>
        /// <param name="txHash">Hash of the transaction to get the receipt for.</param>
        /// <returns>Receipt details for this transaction hash.</returns>
        ReceiptResponse GetReceipt(string txHash);

        /// <summary>
        /// Get the newest block mined on the network.
        /// </summary>
        /// <returns>Newest block mined on the network.</returns>
        Block GetLastBlock();
    }
}
