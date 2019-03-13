using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    public class DepositExtractor : IDepositExtractor
    {
        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILogger logger;

        private readonly Script depositScript;

        public uint MinimumDepositConfirmations { get; private set; }

        public DepositExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            // Note: MultiSigRedeemScript.PaymentScript equals MultiSigAddress.ScriptPubKey
            this.depositScript =
                federationGatewaySettings?.MultiSigRedeemScript?.PaymentScript ??
                federationGatewaySettings?.MultiSigAddress?.ScriptPubKey;
            this.opReturnDataReader = opReturnDataReader;
            this.MinimumDepositConfirmations = federationGatewaySettings.MinimumDepositConfirmations;
        }

        /// <inheritdoc />
        public IReadOnlyList<IDeposit> ExtractDepositsFromBlock(Block block, int blockHeight)
        {
            var deposits = new List<IDeposit>();
            uint256 blockHash = block.GetHash();

            foreach (Transaction transaction in block.Transactions)
            {
                IDeposit deposit = this.ExtractDepositFromTransaction(transaction, blockHeight, blockHash);
                if (deposit != null)
                {
                    deposits.Add(deposit);
                }
            }

            return deposits.AsReadOnly();
        }

        /// <inheritdoc />
        public IDeposit ExtractDepositFromTransaction(Transaction transaction, int blockHeight, uint256 blockHash)
        {
            List<TxOut> depositsToMultisig = transaction.Outputs.Where(output =>
                output.ScriptPubKey == this.depositScript
                && !output.IsDust(FeeRate.Zero)).ToList();

            if (!depositsToMultisig.Any())
                return null;

            if (!this.opReturnDataReader.TryGetTargetAddress(transaction, out string targetAddress))
                return null;

            this.logger.LogInformation("Processing a received deposit transaction with address: {0}. Transaction hash: {1}.",
                targetAddress, transaction.GetHash());

            return new Deposit(transaction.GetHash(), depositsToMultisig.Sum(o => o.Value), targetAddress, blockHeight, blockHash);
        }

        public MaturedBlockDepositsModel ExtractBlockDeposits(ChainedHeaderBlock newlyMaturedBlock)
        {
            if (newlyMaturedBlock == null)
                return null;

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