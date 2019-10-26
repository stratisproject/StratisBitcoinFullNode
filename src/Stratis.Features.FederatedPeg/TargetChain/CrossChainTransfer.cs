using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Tracks the status of the cross-chain transfer.
    /// </summary>
    public class CrossChainTransfer : ICrossChainTransfer
    {
        /// <inheritdoc />
        public uint256 DepositTransactionId => this.depositTransactionId;
        private uint256 depositTransactionId;

        /// <inheritdoc />
        public Script DepositTargetAddress => this.depositTargetAddress;
        private Script depositTargetAddress;

        /// <inheritdoc />
        public long DepositAmount => this.depositAmount;
        private long depositAmount;

        /// <inheritdoc />
        public int? DepositHeight => this.depositHeight;
        private int? depositHeight;

        /// <inheritdoc />
        public Transaction PartialTransaction => this.partialTransaction;
        private Transaction partialTransaction;

        /// <inheritdoc />
        public uint256 BlockHash => this.blockHash;
        private uint256 blockHash;

        /// <inheritdoc />
        public int? BlockHeight => this.blockHeight;
        private int? blockHeight;

        /// <inheritdoc />
        public CrossChainTransferStatus Status => this.status;
        private CrossChainTransferStatus status;

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
        /// <param name="depositTargetAddress">The target address of the deposit transaction.</param>
        /// <param name="depositAmount">The amount (in satoshis) of the deposit transaction.</param>
        /// <param name="depositHeight">The chain A height at which the deposit was made (if known).</param>
        /// <param name="partialTransaction">The unsigned partial transaction containing a full set of available UTXO's.</param>
        /// <param name="blockHash">The hash of the block where the transaction resides.</param>
        /// <param name="blockHeight">The height (in our chain) of the block where the transaction resides.</param>
        public CrossChainTransfer(CrossChainTransferStatus status, uint256 depositTransactionId, Script depositTargetAddress, Money depositAmount,
            int? depositHeight, Transaction partialTransaction, uint256 blockHash = null, int? blockHeight = null)
        {
            this.status = status;
            this.depositTransactionId = depositTransactionId;
            this.depositTargetAddress = depositTargetAddress;
            this.depositAmount = depositAmount;
            this.depositHeight = depositHeight;
            this.partialTransaction = partialTransaction;
            this.blockHash = blockHash;
            this.blockHeight = blockHeight;

            Guard.Assert(this.IsValid());
        }

        /// <summary>
        /// (De)serializes this object.
        /// </summary>
        /// <param name="stream">Stream to use for (de)serialization.</param>
        [NoTrace]
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
            stream.ReadWrite(ref this.depositTargetAddress);
            stream.ReadWrite(ref this.depositAmount);

            int depositHeight = this.DepositHeight ?? -1;
            stream.ReadWrite(ref depositHeight);
            this.depositHeight = (depositHeight < 0) ? (int?)null : depositHeight;

            stream.ReadWrite(ref this.partialTransaction);

            if (!stream.Serializing && this.partialTransaction.Inputs.Count == 0 && this.partialTransaction.Outputs.Count == 0)
                this.partialTransaction = null;

            if (this.status == CrossChainTransferStatus.SeenInBlock)
            {
                uint256 blockHash = this.blockHash ?? 0;
                stream.ReadWrite(ref blockHash);
                this.blockHash = (blockHash == 0) ? null : blockHash;

                int blockHeight = this.BlockHeight ?? -1;
                stream.ReadWrite(ref blockHeight);
                this.blockHeight = (blockHeight < 0) ? (int?)null : blockHeight;
            }
        }

        /// <inheritdoc />
        public bool IsValid()
        {
            if (this.depositTransactionId == null || this.depositTargetAddress == null || this.depositAmount == 0)
                return false;

            if (this.status == CrossChainTransferStatus.Suspended || this.status == CrossChainTransferStatus.Rejected)
                return true;

            if (this.PartialTransaction == null)
                return false;

            if (this.status == CrossChainTransferStatus.SeenInBlock && (this.blockHash == null || this.blockHeight == null))
            {
                return false;
            }

             return true;
        }

        /// <inheritdoc />
        public void SetStatus(CrossChainTransferStatus status, uint256 blockHash = null, int? blockHeight = null)
        {
            this.status = status;

            if (this.status == CrossChainTransferStatus.SeenInBlock)
            {
                this.blockHash = blockHash;
                this.blockHeight = blockHeight;
            }

            Guard.Assert(this.IsValid());
        }

        /// <inheritdoc />
        public int GetSignatureCount(Network network)
        {
            return this.partialTransaction.GetSignatureCount(network);
        }

        /// <inheritdoc />
        public void CombineSignatures(TransactionBuilder builder, Transaction[] partialTransactions)
        {
            Guard.Assert(this.status == CrossChainTransferStatus.Partial);

            this.partialTransaction = SigningUtils.CheckTemplateAndCombineSignatures(builder, this.partialTransaction, partialTransactions);
        }

        /// <inheritdoc />
        public void SetPartialTransaction(Transaction partialTransaction)
        {
            this.partialTransaction = partialTransaction;
        }
    }
}
