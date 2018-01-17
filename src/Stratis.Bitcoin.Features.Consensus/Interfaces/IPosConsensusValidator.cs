using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;

namespace Stratis.Bitcoin.Features.Consensus.Interfaces
{
    /// <summary>
    /// Provides an interface for a consensus validator for verifying validity of PoS block.
    /// </summary>
    public interface IPosConsensusValidator : IPowConsensusValidator
    {
        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        IStakeValidator StakeValidator { get; }

        /// <summary>
        /// Gets miner's coin stake reward.
        /// </summary>
        /// <param name="height">Target block height.</param>
        /// <returns>Miner's coin stake reward.</returns>
        Money GetProofOfStakeReward(int height);
    }
}