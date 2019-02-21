using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;

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

            if (this.status == CrossChainTransferStatus.Suspended)
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

            Guard.Assert(IsValid());
        }

        /// <summary>
        /// Checks whether two transaction have identical inputs and outputs.
        /// </summary>
        /// <param name="partialTransaction1">First transaction.</param>
        /// <param name="partialTransaction2">Second transaction.</param>
        /// <returns><c>True</c> if identical and <c>false</c> otherwise.</returns>
        public static bool TemplatesMatch(Network network, Transaction partialTransaction1, Transaction partialTransaction2)
        {
            if (network.Consensus.IsProofOfStake)
            {
                if (partialTransaction1.Time != partialTransaction2.Time)
                {
                    return false;
                }
            }

            if ((partialTransaction1.Inputs.Count != partialTransaction2.Inputs.Count) ||
                (partialTransaction1.Outputs.Count != partialTransaction2.Outputs.Count))
            {
                return false;
            }

            for (int i = 0; i < partialTransaction1.Inputs.Count; i++)
            {
                TxIn input1 = partialTransaction1.Inputs[i];
                TxIn input2 = partialTransaction2.Inputs[i];

                if ((input1.PrevOut.N != input2.PrevOut.N) || (input1.PrevOut.Hash != input2.PrevOut.Hash))
                {
                    return false;
                }
            }

            for (int i = 0; i < partialTransaction1.Outputs.Count; i++)
            {
                TxOut output1 = partialTransaction1.Outputs[i];
                TxOut output2 = partialTransaction2.Outputs[i];

                if ((output1.Value != output2.Value) || (output1.ScriptPubKey != output2.ScriptPubKey))
                    return false;
            }

            return true;
        }

        /// <inheritdoc />
        public int GetSignatureCount(Network network)
        {
            Guard.NotNull(this.PartialTransaction, nameof(this.PartialTransaction));
            Guard.Assert(this.PartialTransaction.Inputs.Any());

            Script scriptSig = this.PartialTransaction.Inputs[0].ScriptSig;
            if (scriptSig == null)
                return 0;

            // Remove the script from the end.
            scriptSig = new Script(scriptSig.ToOps().SkipLast(1));

            TransactionSignature[] result = PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(network, scriptSig);

            return result?.Count(s => s != null) ?? 0;
        }

        /// <inheritdoc />
        public void CombineSignatures(TransactionBuilder builder, Transaction[] partialTransactions)
        {
            Guard.Assert(this.status == CrossChainTransferStatus.Partial);

            Transaction[] validPartials = partialTransactions.Where(p => TemplatesMatch(builder.Network, p, this.partialTransaction) && p.GetHash() != this.PartialTransaction.GetHash()).ToArray();
            if (validPartials.Any())
            {
                var allPartials = new Transaction[validPartials.Length + 1];
                allPartials[0] = this.partialTransaction;
                validPartials.CopyTo(allPartials, 1);

                this.partialTransaction = builder.CombineSignatures(allPartials);
            }
        }

        /// <inheritdoc />
        public void SetPartialTransaction(Transaction partialTransaction)
        {
            this.partialTransaction = partialTransaction;
        }
    }
}
