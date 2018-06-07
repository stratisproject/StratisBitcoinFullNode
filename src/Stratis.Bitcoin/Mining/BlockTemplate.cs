using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    public sealed class BlockTemplate
    {
        public Block Block;

        public string CoinbaseCommitment;

        public Money TotalFee;

        public BlockTemplate(Network network)
        {
            this.Block = network.Consensus.ConsensusFactory.CreateBlock();
        }
    }
}