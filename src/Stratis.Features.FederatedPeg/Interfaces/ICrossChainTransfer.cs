using NBitcoin;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Cross-chain transfer statuses.
    /// </summary>
    public enum CrossChainTransferStatus
    {
        Suspended = 'U',
        Partial = 'P',
        FullySigned = 'F',
        SeenInBlock = 'S'
    }

    public interface ICrossChainTransfer : IBitcoinSerializable
    {
        /// <summary>
        /// The transaction id of the deposit transaction.
        /// </summary>
        uint256 DepositTransactionId { get; }

        /// <summary>
        /// The target address of the deposit transaction.
        /// </summary>
        Script DepositTargetAddress { get; }

        /// <summary>
        /// The amount (in satoshis) of the deposit transaction.
        /// </summary>
        long DepositAmount { get; }

        /// <summary>
        /// The chain A deposit height of the transaction. Is null if only seen in a block.
        /// </summary>
        int? DepositHeight { get; }

        /// <summary>
        /// The unsigned partial transaction containing a full set of available UTXO's.
        /// </summary>
        Transaction PartialTransaction { get; }

        /// <summary>
        /// The hash of the block where the transaction resides on our chain.
        /// </summary>
        uint256 BlockHash { get; }

        /// <summary>
        /// The height of the block where the transaction resides on our chain.
        /// </summary>
        int? BlockHeight { get; }

        CrossChainTransferStatus Status { get; }

        /// <summary>
        /// Depending on the status some fields can't be null.
        /// </summary>
        /// <returns><c>false</c> if the object is invalid and <c>true</c> otherwise.</returns>
        bool IsValid();

        /// <summary>
        /// Sets the status and verifies that the status is valid given the available fields.
        /// </summary>
        /// <param name="status">The new status.</param>
        /// <param name="blockHash">The block hash of the partialTranction.</param>
        /// <param name="blockHeight">The block height of the partialTransaction.</param>
        void SetStatus(CrossChainTransferStatus status, uint256 blockHash = null, int? blockHeight = null);

        /// <summary>
        /// Combines signatures from partial transactions received from other federation members.
        /// </summary>
        /// <param name="builder">The transaction builder to use.</param>
        /// <param name="partialTransactions">Partial transactions received from other federation members.</param>
        void CombineSignatures(TransactionBuilder builder, Transaction[] partialTransactions);

        /// <summary>
        /// Sets the partial transaction.
        /// </summary>
        /// <param name="partialTransaction">Partial transaction.</param>
        void SetPartialTransaction(Transaction partialTransaction);

        /// <summary>
        /// Gets the number of sigatures in the first input of the transaction.
        /// </summary>
        /// <returns>Number of signatures.</returns>
        int GetSignatureCount(Network network);
    }
}