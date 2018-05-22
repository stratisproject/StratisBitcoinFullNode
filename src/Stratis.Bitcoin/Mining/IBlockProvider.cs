using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    public interface IBlockProvider
    {
        BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script);
        BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script);
    }
}