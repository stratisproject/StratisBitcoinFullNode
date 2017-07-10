using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.RPC.Controllers
{
    public abstract class BaseRPCController : Controller
    {
        protected IFullNode FullNode;
        protected NodeSettings Settings;
        protected Network Network;
        protected PowConsensusValidator ConsensusValidator;
        protected ConsensusLoop ConsensusLoop;
        protected ChainBase Chain;
        protected ChainBehavior.ChainState ChainState;
        protected BlockStoreManager BlockManager;
        protected MempoolManager MempoolManager;
        protected Connection.IConnectionManager ConnectionManager;

        public BaseRPCController(
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            PowConsensusValidator consensusValidator = null,
            ConsensusLoop consensusLoop = null,
            ConcurrentChain chain = null,
            ChainBehavior.ChainState chainState = null,
            BlockStoreManager blockManager = null,
            MempoolManager mempoolManager = null,
            Connection.IConnectionManager connectionManager = null)
        {
            this.FullNode = fullNode;
            this.Settings = nodeSettings;
            this.Network = network;
            this.ConsensusValidator = consensusValidator;
            this.ConsensusLoop = consensusLoop;
            this.Chain = chain;
            this.ChainState = chainState;
            this.BlockManager = blockManager;
            this.MempoolManager = mempoolManager;
            this.ConnectionManager = connectionManager;
        }

    }
}
