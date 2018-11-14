using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Mining
{
    public class MinedBlockInterceptor : IMinedBlockInterceptor
    {
        private readonly Network network;

        public MinedBlockInterceptor(Network network)
        {
            this.network = Guard.NotNull(network, nameof(network));
        }

        public void OnMinedBlock(Block block)
        {
            if (block is PosBlock posBlock)
            {
                ProvenBlockHeader provenBlockHeader = ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(posBlock);
                posBlock.SetHeader(provenBlockHeader);
            }
        }
    }
}