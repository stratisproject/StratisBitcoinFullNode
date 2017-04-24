using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class StakeChainStore : StakeChain
	{
		// TODO: to use the coin view persist logic (store to disk after a trashold)
		// the code to push to DBreezeCoinView can be included in CachedCoinView
		// then when the CachedCoinView flushes all uncommited entreis the stake entries can also
		// be commited (before thre coin view save) from the CachedCoinView to the DBreezeCoinView

		private readonly Network network;
		private readonly ConcurrentChain chain;
		private readonly DBreezeCoinView dBreezeCoinView;
		private readonly AsyncDictionary<uint256, Tuple<long, BlockStake>> items = new AsyncDictionary<uint256, Tuple<long, BlockStake>>();
		private readonly int trashold;

		public StakeChainStore(Network network, ConcurrentChain chain , DBreezeCoinView dBreezeCoinView)
		{
			this.network = network;
			this.chain = chain;
			this.dBreezeCoinView = dBreezeCoinView;
			this.trashold = 5000; // keep this items in memory
		}

		public async Task Load()
		{
			var hash = await this.dBreezeCoinView.GetBlockHashAsync();
			var next = chain.GetBlock(hash);
			while (true)
			{
				var block = await this.dBreezeCoinView.GetStake(next.HashBlock);
				await this.items.TryAdd(next.HashBlock, new Tuple<long, BlockStake>(next.Height, block));
				if (await this.items.Count > this.trashold)
					break;
				next = next.Previous;
				if (next == null)
					break;
			}
		}

		public async Task<BlockStake> GetAsync(uint256 blockid)
		{
			if (this.network.GenesisHash == blockid)
				return new BlockStake(this.network.GetGenesis())
				{
					HashProof = this.network.GenesisHash,
					Flags = BlockFlag.BLOCK_STAKE_MODIFIER
				};

			var block = await this.items.TryGetValue(blockid).ConfigureAwait(false);
			return block?.Item2 ?? await this.dBreezeCoinView.GetStake(blockid).ConfigureAwait(false);
		}

		public override BlockStake Get(uint256 blockid)
		{
			return this.GetAsync(blockid).GetAwaiter().GetResult();
		}

		public async Task SetAsync(uint256 blockid, BlockStake blockStake)
		{
			if(await this.items.ContainsKey(blockid))
				return;

			await this.dBreezeCoinView.PutStake(blockid, blockStake).ConfigureAwait(false);
			var chainedBlock = this.chain.GetBlock(blockid);
			if (await this.items.TryAdd(blockid, new Tuple<long, BlockStake>(chainedBlock.Height, blockStake)).ConfigureAwait(false))
			{
				if (await this.items.Count > this.trashold)
				{
					// pop an item.
					var select = await this.items.Query(q => true);
					var oldes = select.OrderBy(o => o.Value.Item1).First();
					await this.items.Remove(oldes.Key);
				}
			}
		}

		public sealed override void Set(uint256 blockid, BlockStake blockStake)
		{
			this.SetAsync(blockid, blockStake).GetAwaiter().GetResult();
		}
	}
}