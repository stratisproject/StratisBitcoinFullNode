using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    public interface IBlockBuilder
    {
        BlockTemplate Build(Network network, ChainedHeader chainTip, Script script);
    }
}