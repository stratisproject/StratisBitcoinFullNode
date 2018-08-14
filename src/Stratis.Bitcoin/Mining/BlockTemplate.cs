using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    public sealed class BlockTemplate
    {
        public Block Block { get; set; }

        public Money TotalFee { get; set; }

        public BlockTemplate(Network network)
        {
            this.Block = network.CreateBlock();
        }
    }
}