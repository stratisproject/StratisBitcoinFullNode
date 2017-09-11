using NBitcoin;

namespace Stratis.Bitcoin.Base.Deployments
{
    public class NodeDeployments
    {
        private readonly Network network;

        public NodeDeployments(Network network)
        {
            this.network = network;
            this.BIP9 = new ThresholdConditionCache(network.Consensus);

        }
        public ThresholdConditionCache BIP9 { get; }

        public virtual ConsensusFlags GetFlags(ChainedBlock block)
        {
            lock (this.BIP9)
            {
                var states = this.BIP9.GetStates(block.Previous);
                var flags = new ConsensusFlags(block, states, this.network.Consensus);
                return flags;
            }
        }
    }
}
