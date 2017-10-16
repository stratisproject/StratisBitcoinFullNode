using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class StakeChainStore : StakeChain
    {
        // The code to push to DBreezeCoinView can be included in CachedCoinView
        // then when the CachedCoinView flushes all uncommited entreis the stake entries can also
        // be commited (before thre coin view save) from the CachedCoinView to the DBreezeCoinView.

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly DBreezeCoinView dBreezeCoinView;

        private readonly int threshold;
        private readonly int thresholdWindow;
        private readonly ConcurrentDictionary<uint256, StakeItem> items = new ConcurrentDictionary<uint256, StakeItem>();

        private readonly BlockStake genesis;

        public StakeChainStore(Network network, ConcurrentChain chain, DBreezeCoinView dBreezeCoinView, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.chain = chain;
            this.dBreezeCoinView = dBreezeCoinView;
            this.threshold = 5000; // Count of items in memory.
            this.thresholdWindow = Convert.ToInt32(this.threshold * 0.4); // A window threshold.
            this.genesis = new BlockStake(this.network.GetGenesis())
            {
                HashProof = this.network.GenesisHash,
                Flags = BlockFlag.BLOCK_STAKE_MODIFIER
            };
        }

        public async Task Load()
        {
            this.logger.LogTrace("()");

            uint256 hash = await this.dBreezeCoinView.GetBlockHashAsync();
            ChainedBlock next = this.chain.GetBlock(hash);

            var stakeItems = new List<StakeItem>();
            while (next != this.chain.Genesis)
            {
                stakeItems.Add(StakeItem.Query(next.HashBlock, next.Height));
                if ((stakeItems.Count >= this.threshold) || (next.Previous == null))
                    break;
                next = next.Previous;
            }

            await this.dBreezeCoinView.GetStakeItems(stakeItems);

            foreach (StakeItem stakeItem in stakeItems)
                this.items.TryAdd(stakeItem.BlockHash, stakeItem);

            this.logger.LogTrace("(-)");
        }

        public async Task<BlockStake> GetAsync(uint256 blockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            var stakeItem = StakeItem.Query(blockHash);
            await this.dBreezeCoinView.GetStakeItem(stakeItem);

            this.logger.LogTrace("(-)");

            return stakeItem.BlockStake;
        }

        public override BlockStake Get(uint256 blockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            if (this.network.GenesisHash == blockHash)
            {
                this.logger.LogTrace("(-)[GENESIS]:*.{0}='{1}'", nameof(this.genesis.HashProof), this.genesis.HashProof);
                return this.genesis;
            }

            StakeItem stakeItem = this.items.TryGet(blockHash);
            if (stakeItem != null)
            {
                this.logger.LogTrace("(-)[LOADED]:*.{0}='{1}'", nameof(stakeItem.BlockStake.HashProof), stakeItem.BlockStake.HashProof);
                return stakeItem.BlockStake;
            }

            BlockStake blockStake = this.GetAsync(blockHash).GetAwaiter().GetResult();
            if (blockStake != null)
                this.logger.LogTrace("(-):*.{0}='{1}'", nameof(blockStake.HashProof), blockStake.HashProof);

            return blockStake;
        }

        public async Task SetAsync(ChainedBlock chainedBlock, BlockStake blockStake)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:'{4}')", nameof(chainedBlock), chainedBlock, nameof(blockStake), nameof(blockStake.HashProof), blockStake.HashProof);

            if (this.items.ContainsKey(chainedBlock.HashBlock))
            {
                this.logger.LogTrace("(-)[ALREADY_EXISTS]");
                return;
            }

            var stakeItem = StakeItem.Create(chainedBlock.HashBlock, blockStake, chainedBlock.Height);
            bool added = this.items.TryAdd(chainedBlock.HashBlock, stakeItem);
            if (added)
                await this.Flush(false);

            this.logger.LogTrace("(-)");
        }

        public async Task Flush(bool disposeMode)
        {
            this.logger.LogTrace("({0}:{1})", nameof(disposeMode), disposeMode);

            int count = this.items.Count;
            if (disposeMode || (count > this.threshold))
            {
                // Push to store all items that are not already persisted.
                ICollection<StakeItem> entries = this.items.Values;
                await this.dBreezeCoinView.PutStake(entries.Where(w => !w.ExistsInStore));

                if (disposeMode)
                    return;

                // Pop some items to remove a window of 10 % of the threshold.
                ConcurrentDictionary<uint256, StakeItem> select = this.items;
                IEnumerable<KeyValuePair<uint256, StakeItem>> oldes = select.OrderBy(o => o.Value.Height).Take(this.thresholdWindow);
                StakeItem unused;
                foreach (KeyValuePair<uint256, StakeItem> olde in oldes)
                    this.items.TryRemove(olde.Key, out unused);
            }

            this.logger.LogTrace("(-)");
        }

        public sealed override void Set(uint256 blockid, BlockStake blockStake)
        {
            // TODO: A temporary fix till this methods is fixed in NStratis
            // this removed the dependency on chain.

            throw new NotImplementedException();
            //this.SetAsync(blockid, blockStake).GetAwaiter().GetResult();
        }

        public void Set(ChainedBlock chainedBlock, BlockStake blockStake)
        {
            this.SetAsync(chainedBlock, blockStake).GetAwaiter().GetResult();
        }
    }
}