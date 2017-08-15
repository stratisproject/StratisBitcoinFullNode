using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public abstract class BaseRPCController : Controller
    {
        protected IFullNode FullNode;
        protected NodeSettings Settings;
        protected Network Network;
        protected PowConsensusValidator ConsensusValidator;
        protected ConsensusLoop ConsensusLoop;
        protected ChainBase Chain;
        protected ChainState ChainState;
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
            ChainState chainState = null,
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
