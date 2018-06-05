using System;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Controllers
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionDescription : Attribute
    {
        public string Description { get; private set; }

        public ActionDescription(string description)
        {
            this.Description = description;
        }
    }

    public abstract class FeatureController : Controller
    {
        protected IFullNode FullNode;

        protected Network Network;

        protected ChainBase Chain;

        protected IChainState ChainState;

        protected Connection.IConnectionManager ConnectionManager;

        public FeatureController(
            IFullNode fullNode = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            Connection.IConnectionManager connectionManager = null)
        {
            this.FullNode = fullNode;
            this.Network = network;
            this.Chain = chain;
            this.ChainState = chainState;
            this.ConnectionManager = connectionManager;
        }
    }
}