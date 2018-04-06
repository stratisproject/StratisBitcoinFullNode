using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Utilities.Extensions
{
    public static class CoreNodeExtensions
    {
        public static Money CalculateProofOfWorkReward(this CoreNode node, int blockCount)
        {
            var consensusValidator = node.FullNode.NodeService<IPowConsensusValidator>() as PowConsensusValidator;

            var groupedRewardsBySubsidyHalving = Enumerable.Range(0, blockCount)
                .GroupBy(consensusValidator.ConsensusParams.SubsidyHalvingInterval - 1);

            var rewardsPerGroup = new List<Money>();

            foreach(var groupedReward in groupedRewardsBySubsidyHalving)
            {
                rewardsPerGroup.Add(groupedReward.Count() * consensusValidator.GetProofOfWorkReward(groupedReward.Min() + 1));
            }

            return rewardsPerGroup.Sum();
        }

        private static IEnumerable<IGrouping<int, TSource>> GroupBy<TSource> (this IEnumerable<TSource> source, int itemsPerGroup)
        {
            return source.Zip(Enumerable.Range(0, source.Count()),
                              (s, r) => new { Group = r / itemsPerGroup, Item = s })
                         .GroupBy(i => i.Group, g => g.Item)
                         .ToList();
        }
    }
}
