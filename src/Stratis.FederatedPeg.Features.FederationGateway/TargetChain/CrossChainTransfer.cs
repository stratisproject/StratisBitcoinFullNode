using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
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
        public bool DepositPresent => this.depositPresent;
        private bool depositPresent => this.depositTargetAddress != null;

        /// <inheritdoc />
        public long DepositBlockHeight => this.depositBlockHeight;
        private long depositBlockHeight;

        /// <inheritdoc />
        public Script DepositTargetAddress => this.depositTargetAddress;
        private Script depositTargetAddress;

        /// <inheritdoc />
        public long DepositAmount => this.depositAmount;
        private long depositAmount;

        /// <inheritdoc />
        public Transaction PartialTransaction => this.partialTransaction;
        private Transaction partialTransaction;

        /// <inheritdoc />
        public uint256 BlockHash => this.blockHash;
        private uint256 blockHash;

        /// <inheritdoc />
        public int BlockHeight => this.blockHeight;
        private int blockHeight;

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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void SetStatus(CrossChainTransferStatus status)
        {
            this.status = status;

            Guard.Assert(IsValid());
        }

        /// <inheritdoc />
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
