using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public interface IContractTransactionContext
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
        /// The raw data provided as part of the transaction.
        /// </summary>
        byte[] Data { get; }

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
        /// Time as set on transaction.
        /// </summary>
        uint Time { get; }
    }
}
