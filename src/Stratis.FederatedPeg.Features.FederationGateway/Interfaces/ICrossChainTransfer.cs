using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// Cross-chain transfer statuses.
    /// </summary>
    public enum CrossChainTransferStatus
    {
        Partial = 'P',
        FullySigned = 'F',
        SeenInBlock = 'S',
        Rejected = 'R'
    }

    public interface ICrossChainTransfer : IBitcoinSerializable
    {
        /// <summary>
        /// The transaction id of the deposit transaction.
        /// </summary>
        uint256 DepositTransactionId { get; }

        /// <summary>
        /// Indicated whether the deposit fields contain information.
        /// </summary>
        bool DepositPresent { get; }

        /// <summary>
        /// The block height of the deposit transaction.
        /// </summary>
        long DepositBlockHeight { get; }

        /// <summary>
        /// The target address of the deposit transaction.
        /// </summary>
        Script DepositTargetAddress { get; }

        /// <summary>
        /// The amount (in satoshis) of the deposit transaction.
        /// </summary>
        long DepositAmount { get; }

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
        int BlockHeight { get; }

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
        void SetStatus(CrossChainTransferStatus status);

        /// <summary>
        /// Combines signatures from partial transactions received from other federation members.
        /// </summary>
        /// <param name="network">The network targeted by the transactions.</param>
        /// <param name="partials">Partial transactions received from other federation members.</param>
        void CombineSignatures(Network network, Transaction[] partials);
    }
}