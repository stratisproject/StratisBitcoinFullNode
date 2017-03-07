using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool;

using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockStoreCache : IDisposable
	{
		private readonly BlockRepository blockRepository;
		private readonly MemoryCache cache;


		public BlockStoreCache(BlockRepository blockRepository)
		{
			this.blockRepository = blockRepository;

			// use the Microsoft.Extensions.Caching.Memory
			cache = new MemoryCache(new MemoryCacheOptions());
		}

		public void Expire(uint256 blockid)
		{
			// TODO: add code to expire cache on reorg
		}

		public async Task<Block> GetBlockAsync(uint256 blokcid)
		{
			Block block;
			if (this.cache.TryGetValue(blokcid, out block))
				return block;

			block = await this.blockRepository.GetAsync(blokcid);
			if(block != null)
				this.cache.Set(blokcid, block, TimeSpan.FromMinutes(10));

			return block;
		}

		public async Task<Block> GetBlockByTrxAsync(uint256 trxid)
		{
			uint256 blokcid;
			Block block;
			if (this.cache.TryGetValue(trxid, out blokcid))
			{
				block = await this.GetBlockAsync(blokcid);
			}
			else
			{
				blokcid = await this.blockRepository.GetTrxBlockIdAsync(trxid);
				if (blokcid == null)
					return null;
				this.cache.Set(trxid, blokcid, TimeSpan.FromMinutes(10));
				block = await this.GetBlockAsync(blokcid);
			}

			return block;
		}

		public async Task<Transaction> GetTrxAsync(uint256 trxid)
		{
			var block = await this.GetBlockByTrxAsync(trxid);
			return block?.Transactions.Find(t => t.GetHash() == trxid);
		}

		public void Dispose()
		{
			this.cache.Dispose();
		}

	}
}
