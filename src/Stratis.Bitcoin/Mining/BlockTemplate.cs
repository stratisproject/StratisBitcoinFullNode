using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    public sealed class BlockTemplate
    {
        public readonly Block Block;

        public Money TotalFee;

        public BlockTemplate(Network network)
        {
            this.Block = network.Consensus.ConsensusFactory.CreateBlock();
        }
    }
}