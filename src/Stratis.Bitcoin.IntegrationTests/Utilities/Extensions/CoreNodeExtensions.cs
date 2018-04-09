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
        public static Money CalculateProofOfWorkReward(this CoreNode node, int blocksMined)
        {
            var consensusValidator = node.FullNode.NodeService<IPowConsensusValidator>() as PowConsensusValidator;

            var groupedRewardsBySubsidyHalving = Enumerable.Range(node.FullNode.Chain.Height - blocksMined, blocksMined)
                .GroupBy(consensusValidator.ConsensusParams.SubsidyHalvingInterval - 1);

            var rewardsPerGroup = new List<Money>();

            foreach(var groupedReward in groupedRewardsBySubsidyHalving)
            {
                rewardsPerGroup.Add(groupedReward.Count() * consensusValidator.GetProofOfWorkReward(groupedReward.Min() + 1));
            }

            return rewardsPerGroup.Sum();
        }

        private static IEnumerable<IGrouping<int, TItems>> GroupBy<TItems> (this IEnumerable<TItems> items, int itemsPerGroup)
        {
            return items.Zip(Enumerable.Range(0, items.Count()), (value, index) => new { Group = index / itemsPerGroup, Item = value })
                         .GroupBy(i => i.Group, g => g.Item)
                         .ToList();
        }
    }
}
