﻿using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.Deployments
{
    public class NodeDeployments
    {
        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        public ThresholdConditionCache BIP9 { get; }

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private readonly ConcurrentChain chain;

        public NodeDeployments(Network network, ConcurrentChain chain)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));

            this.network = network;
            this.chain = chain;
            this.BIP9 = new ThresholdConditionCache(network.Consensus);
        }

        public virtual DeploymentFlags GetFlags(ChainedBlock block)
        {
            Guard.NotNull(block, nameof(block));

            lock (this.BIP9)
            {
                ThresholdState[] states = this.BIP9.GetStates(block.Previous);
                var flags = new DeploymentFlags(block, states, this.network.Consensus, this.chain);
                return flags;
            }
        }
    }
}
