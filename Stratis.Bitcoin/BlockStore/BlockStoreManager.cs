using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockStoreManager
	{
		private readonly ConcurrentChain chain;
		private readonly ConnectionManager connection;
		public BlockRepository BlockRepository { get; } // public for testing
		private readonly DateTimeProvider dateTimeProvider;
		private readonly NodeArgs nodeArgs;
		public BlockStore.ChainBehavior.ChainState ChainState { get; }

		public BlockStoreManager(ConcurrentChain chain, ConnectionManager connection, BlockRepository blockRepository,
			DateTimeProvider dateTimeProvider, NodeArgs nodeArgs, BlockStore.ChainBehavior.ChainState chainState)
		{
			this.chain = chain;
			this.connection = connection;
			this.BlockRepository = blockRepository;
			this.dateTimeProvider = dateTimeProvider;
			this.nodeArgs = nodeArgs;
			this.ChainState = chainState;
		}

		public Task TryStoreBlock(Block block, bool reorg)
		{
			if (this.nodeArgs.Prune)
				return Task.CompletedTask;

			if (reorg)
			{
				// TODO: delete blocks if reorg
				// this can be done periodically or 
				// on a separate loop not to block consensus
			}
			else
			{
				return this.BlockRepository.PutAsync(block);
			}

			return Task.CompletedTask;
		}

		public Task RelayBlock(uint256 hash)
		{
			if (this.nodeArgs.Prune)
				return Task.CompletedTask;

			if(this.ChainState.IsInitialBlockDownload)
				return Task.CompletedTask;

			var nodes = this.connection.ConnectedNodes;
			if (!nodes.Any())
				return Task.CompletedTask;

			// find all behaviours then start an exclusive task 
			// to add the hash to each local collection
			var behaviours = nodes.Select(s => s.Behavior<BlockStoreBehavior>());
			foreach (var behaviour in behaviours)
				behaviour.BlockHashesToAnnounce.TryAdd(hash, hash);

			return Task.CompletedTask;
		}
	}
}
