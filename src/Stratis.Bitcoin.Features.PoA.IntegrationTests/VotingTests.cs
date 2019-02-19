using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P.Peer;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class VotingTests : IDisposable
    {
        private readonly TestPoANetwork network;

        private readonly PoANodeBuilder builder;

        private readonly CoreNode node1, node2, node3;

        public VotingTests()
        {
            this.network = new TestPoANetwork();

            this.builder = PoANodeBuilder.CreatePoANodeBuilder(this);

            this.node1 = this.builder.CreatePoANode(this.network, this.network.FederationKey1).Start();
            this.node2 = this.builder.CreatePoANode(this.network, this.network.FederationKey2).Start();
            this.node3 = this.builder.CreatePoANode(this.network, this.network.FederationKey3).Start();
        }

        [Fact]
        // Checks that fed members cant vote twice.
        // Checks that miner adds voting data if it exists.
        public async Task CantVoteTwiceAsync()
        {
            int originalFedMembersCount = this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count;

            TestHelper.Connect(this.node1, this.node2);

            await this.node1.MineBlocksAsync(3);

            var model = new HexPubKeyModel() { PubKeyHex = "03025fcadedd28b12665de0542c8096f4cd5af8e01791a4d057f67e2866ca66ba7" };
            this.node1.FullNode.NodeService<VotingController>().VoteAddFedMember(model);

            Assert.Single(this.node1.FullNode.NodeService<VotingManager>().GetScheduledVotes());
            Assert.Empty(this.node1.FullNode.NodeService<VotingManager>().GetPendingPolls());

            await this.node1.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Empty(this.node1.FullNode.NodeService<VotingManager>().GetScheduledVotes());
            Assert.Single(this.node1.FullNode.NodeService<VotingManager>().GetPendingPolls());

            // Vote 2nd time and make sure nothing changed.
            this.node1.FullNode.NodeService<VotingController>().VoteAddFedMember(model);
            await this.node1.MineBlocksAsync(1);
            Assert.Empty(this.node1.FullNode.NodeService<VotingManager>().GetScheduledVotes());
            Assert.Single(this.node1.FullNode.NodeService<VotingManager>().GetPendingPolls());

            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            // Node 2 votes. After that it will be enough to change the federation.
            this.node2.FullNode.NodeService<VotingController>().VoteAddFedMember(model);

            await this.node2.MineBlocksAsync((int)this.network.Consensus.MaxReorgLength + 1);

            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Equal(originalFedMembersCount + 1, this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);
            Assert.Equal(originalFedMembersCount + 1, this.node2.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);

            TestHelper.Connect(this.node2, this.node3);

            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2, this.node3);
        }

        [Fact]
        // Checks that node can sync from scratch if federation voted in favor of adding a new fed member.
        public async Task CanSyncIfFedMemberAddedAsync()
        {
            int originalFedMembersCount = this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count;

            TestHelper.Connect(this.node1, this.node2);

            var model = new HexPubKeyModel() { PubKeyHex = "03025fcadedd28b12665de0542c8096f4cd5af8e01791a4d057f67e2866ca66ba7" };
            this.node1.FullNode.NodeService<VotingController>().VoteAddFedMember(model);
            this.node2.FullNode.NodeService<VotingController>().VoteAddFedMember(model);

            await this.node1.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            await this.node2.MineBlocksAsync((int)this.network.Consensus.MaxReorgLength * 3);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Equal(originalFedMembersCount + 1, this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);

            TestHelper.Connect(this.node2, this.node3);

            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2, this.node3);
        }

        [Fact]
        // Checks that node can sync from scratch if federation voted in favor of kicking a fed member that is syncing.
        public async Task CanSyncIfFedMemberKickedAsync()
        {
            int originalFedMembersCount = this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count;

            TestHelper.Connect(this.node1, this.node2);

            var model = new HexPubKeyModel() { PubKeyHex = this.network.FederationKey2.PubKey.ToHex() };
            this.node1.FullNode.NodeService<VotingController>().VoteKickFedMember(model);
            this.node2.FullNode.NodeService<VotingController>().VoteKickFedMember(model);

            await this.node2.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            await this.node1.MineBlocksAsync((int)this.network.Consensus.MaxReorgLength * 3);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Equal(originalFedMembersCount - 1, this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);

            TestHelper.Connect(this.node2, this.node3);

            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2, this.node3);
        }

        // TODO ensure reorg reverts applying adding fed members

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }
}
