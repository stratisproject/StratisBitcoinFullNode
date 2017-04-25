using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class StakeItem
	{
		public uint256 BlockId;
		public BlockStake BlockStake;
		public bool InStore;
		public long Height;
	}

	public class StakeChainStore : StakeChain
	{
		// the code to push to DBreezeCoinView can be included in CachedCoinView
		// then when the CachedCoinView flushes all uncommited entreis the stake entries can also
		// be commited (before thre coin view save) from the CachedCoinView to the DBreezeCoinView

		private readonly Network network;
		private readonly ConcurrentChain chain;
		private readonly DBreezeCoinView dBreezeCoinView;
		
		private readonly int trashold;
		private readonly int trasholdWindow;

		// this might need to become a concurrent dictionary
		// as it stands its not too safe to call outside of consensus (as consensus is single threaded)
		// if this becomes an issue we'll replace it with a ConcurrentDictionary
		private readonly Dictionary<uint256, StakeItem> items = new Dictionary<uint256, StakeItem>();

		public StakeChainStore(Network network, ConcurrentChain chain , DBreezeCoinView dBreezeCoinView)
		{
			this.network = network;
			this.chain = chain;
			this.dBreezeCoinView = dBreezeCoinView;
			this.trashold = 5000; // count of items in memory
			this.trasholdWindow = Convert.ToInt32(this.trashold*0.4); // a window trashold
		}

		public async Task Load()
		{
			var hash = await this.dBreezeCoinView.GetBlockHashAsync();
			var next = chain.GetBlock(hash);
			var load = new List<StakeItem>();

			while (true)
			{
				load.Add(new StakeItem {BlockId = next.HashBlock, Height = next.Height});
				if (load.Count >= this.trashold || next.Previous == null)
					break;
				next = next.Previous;
			}

			await this.dBreezeCoinView.GetStake(load);
			// all block stake items should be in store
			Guard.Assert(load.All(l => l.BlockStake != null));
			foreach (var stakeItem in load)
				this.items.Add(stakeItem.BlockId, stakeItem);
		}

		private BlockStake Genesis => new BlockStake(this.network.GetGenesis())
		{
			HashProof = this.network.GenesisHash,
			Flags = BlockFlag.BLOCK_STAKE_MODIFIER
		};

		public async Task<BlockStake> GetAsync(uint256 blockid)
		{
			var stakeItem = new StakeItem {BlockId = blockid};
			await this.dBreezeCoinView.GetStake(new [] { stakeItem }).ConfigureAwait(false);
			Guard.Assert(stakeItem.BlockStake != null); // if we ask for it then we expect its in store
			return stakeItem.BlockStake;
		}

		public override BlockStake Get(uint256 blockid)
		{
			if (this.network.GenesisHash == blockid)
				return this.Genesis;

			var block = this.items.TryGet(blockid);
			if (block != null)
				return block.BlockStake;

			return this.GetAsync(blockid).GetAwaiter().GetResult();
		}

		public async Task SetAsync(uint256 blockid, BlockStake blockStake)
		{
			if(this.items.ContainsKey(blockid))
				return;
			
			var chainedBlock = this.chain.GetBlock(blockid);
			var item = new StakeItem {BlockId = blockid, Height = chainedBlock.Height, BlockStake = blockStake, InStore = false};
			var added = this.items.TryAdd(blockid, item);

			if (added)
				await this.Flush(false);
		}

		public async Task Flush(bool disposeMode)
		{
			var count = this.items.Count;
			if (disposeMode || count > this.trashold)
			{
				// push to store all items that are not already persisted
				var entries = this.items.Values;
				await this.dBreezeCoinView.PutStake(entries.Where(w => !w.InStore));

				if(disposeMode)
					return;

				// pop some items remove a window of 10% of the trashold.
				var select = this.items;
				var oldes = select.OrderBy(o => o.Value.Height).Take(this.trasholdWindow); 
				foreach (var olde in oldes)
					this.items.Remove(olde.Key);
			}
		}

		public sealed override void Set(uint256 blockid, BlockStake blockStake)
		{
			this.SetAsync(blockid, blockStake).GetAwaiter().GetResult();
		}
	}
}