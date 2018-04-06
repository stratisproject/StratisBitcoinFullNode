using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Utilities.Extensions
{
    public static class CoreNodeExtensions
    {
        /// <summary>
        /// Calculates the proof of work reward fee taking into account the halving subsidy.
        /// </summary>
        /// <param name="blockCount">Count of blocks minded.</param>
        /// <returns>A <see cref="Money"/> reward.</returns>
        /// <remarks>This will compute the total based on both lower or higher bands.</remarks>
        public static Money CalculateProofOfWorkReward(this CoreNode node, int blockCount)
        {
            var consensusValidator = node.FullNode.NodeService<IPowConsensusValidator>() as PowConsensusValidator;

            Money reward;
            int subsidyHalvingInterval = consensusValidator.ConsensusParams.SubsidyHalvingInterval;

            if (blockCount < subsidyHalvingInterval)
            {
                reward = consensusValidator.GetProofOfWorkReward(blockCount) * blockCount;
            }
            else
            {
                reward = (consensusValidator.GetProofOfWorkReward(subsidyHalvingInterval - 1) * subsidyHalvingInterval)
                            + (consensusValidator.GetProofOfWorkReward(blockCount) * (blockCount - (subsidyHalvingInterval + 1)));
            }

            return reward;
        }
    }
}
