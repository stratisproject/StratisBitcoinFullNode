using System;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;

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
        protected IFullNode FullNode { get; set; }

        protected NodeSettings Settings { get; set; }

        protected Network Network { get; set; }

        protected ChainBase Chain { get; set; }

        protected IChainState ChainState { get; set; }

        protected IConnectionManager ConnectionManager { get; set; }

        protected IConsensusManager ConsensusManager { get; private set; }

        public FeatureController(
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            IConnectionManager connectionManager = null,
            IConsensusManager consensusManager = null)
        {
            this.FullNode = fullNode;
            this.Settings = nodeSettings;
            this.Network = network;
            this.Chain = chain;
            this.ChainState = chainState;
            this.ConnectionManager = connectionManager;
            this.ConsensusManager = consensusManager;
        }
    }
}