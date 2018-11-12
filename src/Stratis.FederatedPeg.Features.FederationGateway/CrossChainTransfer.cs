using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
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

    /// <summary>
    /// Tracks the status of the cross-chain transfer.
    /// </summary>
    public class CrossChainTransfer : IBitcoinSerializable
    {
        /// <summary>
        /// The transaction id of the deposit transaction.
        /// </summary>
        private uint256 depositTransactionId;
        public uint256 DepositTransactionId => this.depositTransactionId;

        /// <summary>
        /// Indicated whether the deposit fields contain information.
        /// </summary>
        private bool depositPresent => this.depositTargetAddress != null;
        public bool DepositPresent => this.depositPresent;

        /// <summary>
        /// The block height of the deposit transaction.
        /// </summary>
        public long DepositBlockHeight => this.depositBlockHeight;
        private long depositBlockHeight;

        /// <summary>
        /// The target address of the deposit transaction.
        /// </summary>
        public Script DepositTargetAddress => this.depositTargetAddress;
        private Script depositTargetAddress;

        /// <summary>
        /// The amount (in satoshis) of the deposit transaction.
        /// </summary>
        public long DepositAmount => this.depositAmount;
        private long depositAmount;

        /// <summary>
        /// The unsigned partial transaction containing a full set of available UTXO's.
        /// </summary>
        public Transaction PartialTransaction => this.partialTransaction;
        private Transaction partialTransaction;

        /// <summary>
        /// The hash of the block where the transaction resides on our chain.
        /// </summary>
        public uint256 BlockHash => this.blockHash;
        private uint256 blockHash;

        /// <summary>
        /// The height of the block where the transaction resides on our chain.
        /// </summary>
        public int BlockHeight => this.blockHeight;
        private int blockHeight;

        /// <summary>
        /// The status of the cross chain transfer transaction.
        /// </summary>
        private CrossChainTransferStatus status;
        public CrossChainTransferStatus Status => this.status;

        /// <summary>
        /// Parameter-less constructor for (de)serialization.
        /// </summary>
        public CrossChainTransfer()
        {
        }

        /// <summary>
        /// Constructs this object from passed parameters.
        /// </summary>
        /// <param name="status">The status of the cross chain transfer transaction.</param>
        /// <param name="depositTransactionId">The transaction id of the deposit transaction.</param>
        /// <param name="depositBlockHeight">The block height of the deposit transaction.</param>
        /// <param name="depositTargetAddress">The target address of the deposit transaction.</param>
        /// <param name="depositAmount">The amount (in satoshis) of the deposit transaction.</param>
        /// <param name="partialTransaction">The unsigned partial transaction containing a full set of available UTXO's.</param>
        /// <param name="blockHash">The hash of the block where the transaction resides.</param>
        /// <param name="blockHeight">The height (in our chain) of the block where the transaction resides.</param>
        public CrossChainTransfer(CrossChainTransferStatus status, uint256 depositTransactionId, long depositBlockHeight, Script depositTargetAddress, Money depositAmount,
            Transaction partialTransaction, uint256 blockHash, int blockHeight)
        {
            this.status = status;
            this.depositTransactionId = depositTransactionId;
            this.depositBlockHeight = depositBlockHeight;
            this.depositTargetAddress = depositTargetAddress;
            this.depositAmount = depositAmount;
            this.partialTransaction = partialTransaction;
            this.blockHash = blockHash;
            this.blockHeight = blockHeight;

            Guard.Assert(this.IsValid());
        }

        /// <summary>
        /// (De)serializes this object.
        /// </summary>
        /// <param name="stream">Stream to use for (de)serialization.</param>
        public void ReadWrite(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                Guard.Assert(this.IsValid());
            }

            byte status = (byte)this.status;
            stream.ReadWrite(ref status);
            this.status = (CrossChainTransferStatus)status;
            stream.ReadWrite(ref this.depositTransactionId);

            bool depositPresent = this.depositPresent;
            stream.ReadWrite(ref depositPresent);

            if (depositPresent)
            {
                stream.ReadWrite(ref this.depositBlockHeight);
                stream.ReadWrite(ref this.depositTargetAddress);
                stream.ReadWrite(ref this.depositAmount);
            }

            if (this.status == CrossChainTransferStatus.Partial || this.status == CrossChainTransferStatus.SeenInBlock)
            {
                stream.ReadWrite(ref this.partialTransaction);
                if (this.status != CrossChainTransferStatus.Partial)
                    stream.ReadWrite(ref this.blockHash);
            }
        }

        /// <summary>
        /// Depending on the status some fields can't be null.
        /// </summary>
        /// <returns><c>false</c> if the object is invalid and <c>true</c> otherwise.</returns>
        public bool IsValid()
        {
            if (this.status == CrossChainTransferStatus.Partial || this.status == CrossChainTransferStatus.SeenInBlock)
            {
                if (this.status != CrossChainTransferStatus.Partial)
                {
                    return this.blockHash != null;
                }

                return this.partialTransaction != null;
            }

            return this.depositTransactionId != null;
        }

        /// <summary>
        /// Sets the status and verifies that the status is valid given the available fields.
        /// </summary>
        /// <param name="status">The new status.</param>
        public void SetStatus(CrossChainTransferStatus status)
        {
            this.status = status;

            Guard.Assert(IsValid());
        }

        /// <summary>
        /// Combines signatures from partial transactions received from other federation members.
        /// </summary>
        /// <param name="network">The network targeted by the transactions.</param>
        /// <param name="partials">Partial transactions received from other federation members.</param>
        public void CombineSignatures(Network network, Transaction[] partials)
        {
            Guard.Assert(this.status == CrossChainTransferStatus.Partial);

            TransactionBuilder builder = new TransactionBuilder(network);

            Transaction[] allPartials = new Transaction[partials.Length + 1];
            allPartials[0] = this.partialTransaction;
            partials.CopyTo(allPartials, 1);

            this.partialTransaction = builder.CombineSignatures(allPartials);
        }
    }
}
