using System.Threading.Tasks;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class EnableVoteKickingTests
    {

        [Fact]
        public async Task EnableAutoKick()
        {
            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                const int idleTimeSeconds = 5 * 60;

                // Have a network that mimics Cirrus where voting is on and kicking is off.
                var votingNetwork = new TestPoANetwork();
                var oldOptions = (PoAConsensusOptions)votingNetwork.Consensus.Options;
                votingNetwork.Consensus.Options = new PoAConsensusOptions(maxBlockBaseSize: oldOptions.MaxBlockBaseSize,
                    maxStandardVersion: oldOptions.MaxStandardVersion,
                    maxStandardTxWeight: oldOptions.MaxStandardTxWeight,
                    maxBlockSigopsCost: oldOptions.MaxBlockSigopsCost,
                    maxStandardTxSigopsCost: oldOptions.MaxStandardTxSigopsCost,
                    genesisFederationMembers: oldOptions.GenesisFederationMembers,
                    targetSpacingSeconds: 60,
                    votingEnabled: true,
                    autoKickIdleMembers: false,
                    federationMemberMaxIdleTimeSeconds: oldOptions.FederationMemberMaxIdleTimeSeconds);

                CoreNode node1 = builder.CreatePoANode(votingNetwork, votingNetwork.FederationKey1).Start();
                CoreNode node2 = builder.CreatePoANode(votingNetwork, votingNetwork.FederationKey2).Start();
                TestHelper.Connect(node1, node2);

                // Mine a block on this network from each node. Confirm it's alive.
                await node1.MineBlocksAsync(1);
                CoreNodePoAExtensions.WaitTillSynced(node1, node2);
                await node2.MineBlocksAsync(1);
                CoreNodePoAExtensions.WaitTillSynced(node1, node2);

                // Edit the consensus options so that kicking is turned on.
                votingNetwork.Consensus.Options = new PoAConsensusOptions(maxBlockBaseSize: oldOptions.MaxBlockBaseSize,
                    maxStandardVersion: oldOptions.MaxStandardVersion,
                    maxStandardTxWeight: oldOptions.MaxStandardTxWeight,
                    maxBlockSigopsCost: oldOptions.MaxBlockSigopsCost,
                    maxStandardTxSigopsCost: oldOptions.MaxStandardTxSigopsCost,
                    genesisFederationMembers: oldOptions.GenesisFederationMembers,
                    targetSpacingSeconds: 60,
                    votingEnabled: true,
                    autoKickIdleMembers: true,
                    federationMemberMaxIdleTimeSeconds: idleTimeSeconds);

                node1.Restart();

                // Lets get our 2 nodes to actively mine some blocks. 
                // In doing so, their test datetimeprovider will increment by minutes at a time.
                for (int i = 0; i < 5; i++)
                {
                    await node1.MineBlocksAsync(1);
                    CoreNodePoAExtensions.WaitTillSynced(node1, node2);

                    await node2.MineBlocksAsync(1);
                    CoreNodePoAExtensions.WaitTillSynced(node1, node2);
                }

                // Enough time has passed - Check that our new node wants to vote the third fed member out, who has not mined at all.
                // Check that we have a single active poll to vote him out.
                var activePolls = node1.FullNode.NodeService<VotingManager>().GetPendingPolls();
                Assert.Single(activePolls);
                Assert.Equal(VoteKey.KickFederationMember, activePolls[0].VotingData.Key);
                byte[] lastMemberBytes = (votingNetwork.Consensus.ConsensusFactory as PoAConsensusFactory).SerializeFederationMember(votingNetwork.ConsensusOptions.GenesisFederationMembers[2]);
                Assert.Equal(lastMemberBytes, activePolls[0].VotingData.Data);
            }
        }
    }
}
