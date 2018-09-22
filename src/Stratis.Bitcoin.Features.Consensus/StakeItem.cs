using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class StakeItem
    {
        public uint256 BlockId;

        public BlockStake BlockStake;

        public ProvenBlockHeader ProvenBlockHeader;

        public bool InStore;

        public long Height;
    }
}
