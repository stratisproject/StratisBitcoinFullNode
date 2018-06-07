using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class PowBlockDefinition : BlockDefinition
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public PowBlockDefinition(
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            BlockDefinitionOptions options = null)
            : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
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

            var context = new RuleContext(new BlockValidationContext { Block = this.block }, this.Network.Consensus, this.ConsensusLoop.Tip)
            {
                CheckPow = false,
                CheckMerkleRoot = false,
            };

            this.ConsensusLoop.ValidateBlock(context);

            this.logger.LogTrace("(-)");
        }
    }
}