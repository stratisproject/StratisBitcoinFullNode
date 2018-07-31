using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class PowBlockDefinition : BlockDefinition
    {
        private readonly IConsensusRules consensusRules;
        private readonly ILogger logger;

        public PowBlockDefinition(
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            MinerSettings minerSettings,
            Network network,
            IConsensusRules consensusRules,
            BlockDefinitionOptions options = null)
            : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            this.logger.LogTrace("({0}.{1}:'{2}', {3}:{4})", nameof(mempoolEntry), nameof(mempoolEntry.TransactionHash), mempoolEntry.TransactionHash, nameof(mempoolEntry.ModifiedFee), mempoolEntry.ModifiedFee);

            this.AddTransactionToBlock(mempoolEntry.Transaction);
            this.UpdateBlockStatistics(mempoolEntry);
            this.UpdateTotalFees(mempoolEntry.Fee);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(chainTip), chainTip, nameof(scriptPubKey), nameof(scriptPubKey.Length), scriptPubKey.Length);

            base.OnBuild(chainTip, scriptPubKey);

            this.TestBlockValidity();

            this.logger.LogTrace("(-)");

            return this.BlockTemplate;
        }

        /// <inheritdoc/>
        public override void UpdateHeaders()
        {
            this.logger.LogTrace("()");

            base.UpdateBaseHeaders();

            this.block.Header.Bits = this.block.Header.GetWorkRequired(this.Network, this.ChainTip);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Before the block gets mined, we need to ensure that it is structurally valid, otherwise a lot of work might be
        /// done for no reason.
        /// </summary>
        public void TestBlockValidity()
        {
            this.logger.LogTrace("()");

            RuleContext context = this.consensusRules.CreateRuleContext(new ValidationContext { Block = this.block }, this.ConsensusLoop.Tip);
            context.MinedBlock = true;

            this.ConsensusLoop.ValidateBlock(context);

            this.logger.LogTrace("(-)");
        }
    }
}