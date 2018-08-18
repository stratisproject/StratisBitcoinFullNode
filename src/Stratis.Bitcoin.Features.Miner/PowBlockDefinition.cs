﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class PowBlockDefinition : BlockDefinition
    {
        private readonly IConsensusRuleEngine consensusRules;
        private readonly ILogger logger;

        public PowBlockDefinition(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            MinerSettings minerSettings,
            Network network,
            IConsensusRuleEngine consensusRules,
            BlockDefinitionOptions options = null)
            : base(consensusManager, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network)
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

            // TODO ACTIVATION pass header of a block that is being validated, not CT!
            //RuleContext context = this.consensusRules.CreateRuleContext(new ValidationContext { Block = this.block, ChainTipToExtend = this.ConsensusManager.Tip });
            //context.MinedBlock = true;
            //
            //// TODO: Is this correct or should we not call validation from rules but from CM
            //this.ConsensusManager.ConsensusRules.PartialValidationAsync(new ValidationContext { Block = this.block, ChainTipToExtend = this.ConsensusManager.Tip });

            this.logger.LogTrace("(-)");
        }
    }
}