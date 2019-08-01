using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    [NoTrace]
    public class DepositExtractor : IDepositExtractor
    {
        /// <summary>
        /// This deposit extractor implementation only looks for a very specific deposit format.
        /// Deposits will have 2 outputs when there is no change.
        /// </summary>
        private const int ExpectedNumberOfOutputsNoChange = 2;

        /// <summary>
        /// Deposits will have 3 outputs when there is change.
        /// </summary>
        private const int ExpectedNumberOfOutputsChange = 3;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILogger logger;

        private readonly Script depositScript;

        public uint MinimumDepositConfirmations { get; private set; }

        public DepositExtractor(
            ILoggerFactory loggerFactory,
            IFederatedPegSettings federatedPegSettings,
            IOpReturnDataReader opReturnDataReader)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            // Note: MultiSigRedeemScript.PaymentScript equals MultiSigAddress.ScriptPubKey
            this.depositScript =
                federatedPegSettings.MultiSigRedeemScript?.PaymentScript ??
                federatedPegSettings.MultiSigAddress?.ScriptPubKey;
            this.opReturnDataReader = opReturnDataReader;
            this.MinimumDepositConfirmations = federatedPegSettings.MinimumDepositConfirmations;
        }

        /// <inheritdoc />
        public IReadOnlyList<IDeposit> ExtractDepositsFromBlock(Block block, int blockHeight)
        {
            var deposits = new List<IDeposit>();

            // If it's an empty block, there's no deposits inside.
            if (block.Transactions.Count <= 1)
                return deposits;

            uint256 blockHash = block.GetHash();

            foreach (Transaction transaction in block.Transactions)
            {
                IDeposit deposit = this.ExtractDepositFromTransaction(transaction, blockHeight, blockHash);
                if (deposit != null)
                {
                    deposits.Add(deposit);
                }
            }

            return deposits;
        }

        /// <inheritdoc />
        public IDeposit ExtractDepositFromTransaction(Transaction transaction, int blockHeight, uint256 blockHash)
        {
            // Coinbases can't have deposits.
            if (transaction.IsCoinBase)
                return null;

            // Deposits have a certain structure.
            if (transaction.Outputs.Count != ExpectedNumberOfOutputsNoChange
                && transaction.Outputs.Count != ExpectedNumberOfOutputsChange)
                return null;

            List<TxOut> depositsToMultisig = transaction.Outputs.Where(output =>
                output.ScriptPubKey == this.depositScript
                && output.Value >= FederatedPegSettings.CrossChainTransferMinimum).ToList();

            if (!depositsToMultisig.Any())
                return null;

            if (!this.opReturnDataReader.TryGetTargetAddress(transaction, out string targetAddress))
                return null;

            this.logger.LogDebug("Processing a received deposit transaction with address: {0}. Transaction hash: {1}.",
                targetAddress, transaction.GetHash());

            return new Deposit(transaction.GetHash(), depositsToMultisig.Sum(o => o.Value), targetAddress, blockHeight, blockHash);
        }

        public MaturedBlockDepositsModel ExtractBlockDeposits(ChainedHeaderBlock newlyMaturedBlock)
        {
            Guard.NotNull(newlyMaturedBlock, nameof(newlyMaturedBlock));

            var maturedBlock = new MaturedBlockInfoModel()
            {
                BlockHash = newlyMaturedBlock.ChainedHeader.HashBlock,
                BlockHeight = newlyMaturedBlock.ChainedHeader.Height,
                BlockTime = newlyMaturedBlock.ChainedHeader.Header.Time
            };

            IReadOnlyList<IDeposit> deposits =
                this.ExtractDepositsFromBlock(newlyMaturedBlock.Block, newlyMaturedBlock.ChainedHeader.Height);

            var maturedBlockDeposits = new MaturedBlockDepositsModel(maturedBlock, deposits);

            return maturedBlockDeposits;
        }
    }
}