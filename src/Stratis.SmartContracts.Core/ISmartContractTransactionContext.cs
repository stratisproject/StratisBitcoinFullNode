using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractTransactionContext
    {
        /// <summary>
        /// Hash of the currently executing transaction.
        /// </summary>
        uint256 TransactionHash { get; }

        /// <summary>
        /// Address of the sender for the current contract call.
        /// </summary>
        uint160 Sender { get; }

        /// <summary>
        /// Value of the smart contract TxOut.
        /// </summary>
        ulong TxOutValue { get; }

        /// <summary>
        /// Index of the smart contract TxOut in the transaction outputs.
        /// </summary>
        uint Nvout { get; }

        /// <summary>
        /// All of the bytes included in the script after the create or call opcode.
        /// </summary>
        IEnumerable<byte> ContractData { get; }

        /// <summary>
        /// The script pub key
        /// </summary>
        Script ScriptPubKey { get; }

        /// <summary>
        /// Total fee for transaction.
        /// </summary>
        Money MempoolFee { get; }

        /// <summary>
        /// Address of the coinbase for the current block.
        /// </summary>
        uint160 CoinbaseAddress { get; }

        /// <summary>
        /// Height of the current block in the chain.
        /// </summary>
        ulong BlockHeight { get; }

        /// <summary>
        /// Whether this transaction is creating a new contract.
        /// </summary>
        bool IsCreate { get; }

        /// <summary>
        /// Whether this transaction is calling a method on a contract.
        /// </summary>
        bool IsCall { get; }
    }
}
