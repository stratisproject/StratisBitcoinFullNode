using NBitcoin;

namespace Stratis.Bitcoin.Mining
{
    public interface IBlockBuilder
    {
        BlockTemplate Build(BlockBuilderMode mode, ChainedHeader chainTip, Script script);
    }

    public enum BlockBuilderMode
    {
        Mining = 0,
        Staking
    }
}