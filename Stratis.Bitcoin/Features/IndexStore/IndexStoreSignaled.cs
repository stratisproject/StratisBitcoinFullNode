﻿using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreSignaled: BlockStoreSignaled
    {
        public IndexStoreSignaled(IndexStoreLoop storeLoop, ConcurrentChain chain, NodeSettings nodeArgs,
            ChainState chainState, IConnectionManager connection,
            INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory):
            base(storeLoop, chain, nodeArgs, chainState, connection, nodeLifetime, asyncLoopFactory, "IndexStore")
        {
        }
    }
}
