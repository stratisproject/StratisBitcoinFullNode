using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class StakeChainStore : IStakeChain
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
            this.genesis = BlockStake.Load(this.network.GetGenesis());
            this.genesis.HashProof = this.network.GenesisHash;
            this.genesis.Flags = BlockFlag.BLOCK_STAKE_MODIFIER;
        }

        public async Task LoadAsync()
        {
            uint256 hash = await this.dBreezeCoinView.GetTipHashAsync().ConfigureAwait(false);
            ChainedHeader currentHeader = this.chain.GetBlock(hash);

            while (currentHeader == null)
            {
                hash = await this.dBreezeCoinView.RewindAsync().ConfigureAwait(false);
                currentHeader = this.chain.GetBlock(hash);
            }

            var load = new List<StakeItem>();

            while (currentHeader != this.chain.Genesis)
            {
                load.Add(new StakeItem { BlockId = currentHeader.HashBlock, Height = currentHeader.Height });
                if ((load.Count >= this.threshold) || (currentHeader.Previous == null))
                    break;
                currentHeader = currentHeader.Previous;
            }

            await this.dBreezeCoinView.GetStakeAsync(load).ConfigureAwait(false);

            // All block stake items should be in store.
            if (load.Any(l => l.BlockStake == null))
            {
                this.logger.LogTrace("(-)[STAKE_INFO_MISSING]");
                throw new ConfigurationException("Missing stake information, delete the data folder and re-download the chain");
            }

            foreach (StakeItem stakeItem in load)
                this.items.TryAdd(stakeItem.BlockId, stakeItem);
        }

        public async Task<BlockStake> GetAsync(uint256 blockid)
        {
            var stakeItem = new StakeItem { BlockId = blockid };
            await this.dBreezeCoinView.GetStakeAsync(new[] { stakeItem }).ConfigureAwait(false);
            
            Guard.Assert(stakeItem.BlockStake != null); // if we ask for it then we expect its in store
            return stakeItem.BlockStake;
        }

        public virtual BlockStake Get(uint256 blockid)
        {
            if (this.network.GenesisHash == blockid)
            {
                this.logger.LogTrace("(-)[GENESIS]:*.{0}='{1}'", nameof(this.genesis.HashProof), this.genesis.HashProof);
                return this.genesis;
            }

            StakeItem block = this.items.TryGet(blockid);
            if (block != null)
            {
                this.logger.LogTrace("(-)[LOADED]:*.{0}='{1}'", nameof(block.BlockStake.HashProof), block.BlockStake.HashProof);
                return block.BlockStake;
            }

            BlockStake res = this.GetAsync(blockid).GetAwaiter().GetResult();
            return res;
        }

        public async Task SetAsync(ChainedHeader chainedHeader, BlockStake blockStake)
        {
            if (this.items.ContainsKey(chainedHeader.HashBlock))
            {
                this.logger.LogTrace("(-)[ALREADY_EXISTS]");
                return;
            }

            //var chainedHeader = this.chain.GetBlock(blockid);
            var item = new StakeItem { BlockId = chainedHeader.HashBlock, Height = chainedHeader.Height, BlockStake = blockStake, InStore = false };
            bool added = this.items.TryAdd(chainedHeader.HashBlock, item);
            if (added)
                await this.FlushAsync(false).ConfigureAwait(false);
        }

        public async Task FlushAsync(bool disposeMode)
        {
            int count = this.items.Count;
            if (disposeMode || (count > this.threshold))
            {
                // Push to store all items that are not already persisted.
                ICollection<StakeItem> entries = this.items.Values;
                await this.dBreezeCoinView.PutStakeAsync(entries.Where(w => !w.InStore)).ConfigureAwait(false);

                if (disposeMode)
                    return;

                // Pop some items to remove a window of 10 % of the threshold.
                ConcurrentDictionary<uint256, StakeItem> select = this.items;
                IEnumerable<KeyValuePair<uint256, StakeItem>> oldes = select.OrderBy(o => o.Value.Height).Take(this.thresholdWindow);
                StakeItem unused;
                foreach (KeyValuePair<uint256, StakeItem> olde in oldes)
                    this.items.TryRemove(olde.Key, out unused);
            }
        }
    }
}
