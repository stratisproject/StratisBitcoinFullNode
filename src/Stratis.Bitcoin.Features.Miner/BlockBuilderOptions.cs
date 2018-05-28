using NBitcoin;

namespace Stratis.Bitcoin.Features.Miner
{
    public sealed class BlockBuilderOptions
    {
        public long BlockMaxWeight = PowMining.DefaultBlockMaxWeight;

        public long BlockMaxSize = PowMining.DefaultBlockMaxSize;

        public FeeRate BlockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);

        public bool IsProofOfStake = false;
    }
}