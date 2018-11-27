using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class DepositExtractor : IDepositExtractor
    {
        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILogger logger;

        private readonly Script depositScript;

        private readonly ConcurrentChain chain;

        public uint MinimumDepositConfirmations { get; private set; }

        public DepositExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader,
            IFullNode fullNode)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.depositScript = federationGatewaySettings.MultiSigRedeemScript;
            this.opReturnDataReader = opReturnDataReader;
            this.MinimumDepositConfirmations = federationGatewaySettings.MinimumDepositConfirmations;
            this.chain = fullNode.NodeService<ConcurrentChain>();
        }

        /// <inheritdoc />
        public IReadOnlyList<IDeposit> ExtractDepositsFromBlock(Block block, int blockHeight)
        {
            var deposits = new List<IDeposit>();
            uint256 blockHash = block.GetHash();

            foreach (Transaction transaction in block.Transactions)
            {
                IDeposit deposit = ExtractDepositFromTransaction(transaction, blockHeight, blockHash);
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
            var depositsToMultisig = transaction.Outputs.Where(output =>
                output.ScriptPubKey == this.depositScript
                && !output.IsDust(FeeRate.Zero)).ToList();

            if (!depositsToMultisig.Any())
                return null;

            var targetAddress = this.opReturnDataReader.TryGetTargetAddress(transaction);
            if (string.IsNullOrWhiteSpace(targetAddress))
                return null;

            this.logger.LogInformation("Processing received transaction with address: {0}. Transaction hash: {1}.",
                targetAddress, transaction.GetHash());

            return new Deposit(transaction.GetHash(), depositsToMultisig.Sum(o => o.Value), targetAddress, blockHeight, blockHash);
        }

        public IMaturedBlockDeposits ExtractMaturedBlockDeposits(ChainedHeader chainedHeader)
        {
            ChainedHeader newlyMaturedBlock = this.GetNewlyMaturedBlock(chainedHeader);

            if (newlyMaturedBlock == null) return null;

            var maturedBlock = new MaturedBlockModel()
            {
                BlockHash = newlyMaturedBlock.HashBlock,
                BlockHeight = newlyMaturedBlock.Height
            };

            IReadOnlyList<IDeposit> deposits =
                this.ExtractDepositsFromBlock(newlyMaturedBlock.Block, newlyMaturedBlock.Height);

            var maturedBlockDeposits = new MaturedBlockDepositsModel(maturedBlock, deposits);

            return maturedBlockDeposits;
        }

        private ChainedHeader GetNewlyMaturedBlock(ChainedHeader chainedHeader)
        {
            if ((this.chain.Tip.Height - chainedHeader.Height) < this.MinimumDepositConfirmations)
                return null;

            return this.chain.GetBlock(chainedHeader.Height);
        }
    }
}