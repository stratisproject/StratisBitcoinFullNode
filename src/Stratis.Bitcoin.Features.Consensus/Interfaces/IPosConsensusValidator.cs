using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;

namespace Stratis.Bitcoin.Features.Consensus.Interfaces
{
    public interface IPosConsensusValidator : IPowConsensusValidator
    {
        IStakeValidator StakeValidator { get; }

        Money GetProofOfStakeReward(int height);
    }
}