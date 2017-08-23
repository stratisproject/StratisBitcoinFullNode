using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public abstract class BaseRPCController : Controller
    {
        protected IFullNode FullNode;
        protected NodeSettings Settings;
        protected Network Network;
        protected ChainBase Chain;
        protected ChainState ChainState;
        protected Connection.IConnectionManager ConnectionManager;

        public BaseRPCController(
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            ChainState chainState = null,
            Connection.IConnectionManager connectionManager = null)
        {
            this.FullNode = fullNode;
            this.Settings = nodeSettings;
            this.Network = network;
            this.Chain = chain;
            this.ChainState = chainState;
            this.ConnectionManager = connectionManager;
        }
    }
}
