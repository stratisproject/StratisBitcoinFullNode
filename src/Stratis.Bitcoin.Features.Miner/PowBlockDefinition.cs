using System.Collections.Generic;
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

        // Container for tracking updates to ancestor feerate as we include (parent)
        // transactions in a block.
        public class TxMemPoolModifiedEntry
        {
            public TxMemPoolModifiedEntry(TxMempoolEntry entry)
            {
                this.iter = entry;
                this.SizeWithAncestors = entry.SizeWithAncestors;
                this.ModFeesWithAncestors = entry.ModFeesWithAncestors;
                this.SigOpCostWithAncestors = entry.SigOpCostWithAncestors;
            }

            public TxMempoolEntry iter;

            public long SizeWithAncestors;

            public Money ModFeesWithAncestors;

            public long SigOpCostWithAncestors;
        }

        // This matches the calculation in CompareTxMemPoolEntryByAncestorFee,
        // except operating on CTxMemPoolModifiedEntry.
        // TODO: Refactor to avoid duplication of this logic.
        public class CompareModifiedEntry : IComparer<TxMemPoolModifiedEntry>
        {
            public int Compare(TxMemPoolModifiedEntry a, TxMemPoolModifiedEntry b)
            {
                Money f1 = a.ModFeesWithAncestors * b.SizeWithAncestors;
                Money f2 = b.ModFeesWithAncestors * a.SizeWithAncestors;

                if (f1 == f2)
                    return TxMempool.CompareIteratorByHash.InnerCompare(a.iter, b.iter);

                return f1 > f2 ? 1 : -1;
            }
        }

        // A comparator that sorts transactions based on number of ancestors.
        // This is sufficient to sort an ancestor package in an order that is valid
        // to appear in a block.
        public class CompareTxIterByAncestorCount : IComparer<TxMempoolEntry>
        {
            public int Compare(TxMempoolEntry a, TxMempoolEntry b)
            {
                if (a.CountWithAncestors != b.CountWithAncestors)
                    return a.CountWithAncestors < b.CountWithAncestors ? -1 : 1;

                return TxMempool.CompareIteratorByHash.InnerCompare(a, b);
            }
        }

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

            this.OnBuild(chainTip, scriptPubKey);

            this.logger.LogTrace("(-)");

            return this.BlockTemplate;
        }

        public override void OnUpdateHeaders()
        {
            this.logger.LogTrace("()");

            this.block.Header.HashPrevBlock = this.ChainTip.HashBlock;
            this.block.Header.UpdateTime(this.DateTimeProvider.GetTimeOffset(), this.Network, this.ChainTip);
            this.block.Header.Bits = this.block.Header.GetWorkRequired(this.Network, this.ChainTip);
            this.block.Header.Nonce = 0;

            this.logger.LogTrace("(-)");
        }

        public override void OnTestBlockValidity()
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