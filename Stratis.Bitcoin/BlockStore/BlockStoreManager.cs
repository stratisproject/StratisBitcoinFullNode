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
		private readonly IConnectionManager connection;
		public BlockRepository BlockRepository { get; } // public for testing
		public BlockStoreLoop BlockStoreLoop { get; } // public for testing

		private readonly IDateTimeProvider dateTimeProvider;
		private readonly NodeSettings nodeArgs;
		public BlockStore.ChainBehavior.ChainState ChainState { get; }

		public BlockStoreManager(ConcurrentChain chain, IConnectionManager connection, BlockRepository blockRepository,
            IDateTimeProvider dateTimeProvider, NodeSettings nodeArgs, BlockStore.ChainBehavior.ChainState chainState, BlockStoreLoop blockStoreLoop)
		{
			this.chain = chain;
			this.connection = connection;
			this.BlockRepository = blockRepository;
			this.dateTimeProvider = dateTimeProvider;
			this.nodeArgs = nodeArgs;
			this.ChainState = chainState;
			this.BlockStoreLoop = blockStoreLoop;
		}
	}
}
