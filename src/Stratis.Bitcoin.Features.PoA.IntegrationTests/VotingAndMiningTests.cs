using System;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.Utils;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class VotingAndMiningTests : IDisposable
    {
        private readonly TestPoANetwork network;

        private readonly PoANodeBuilder builder;

        private readonly CoreNode node1, node2, node3;

        private readonly PubKey testPubKey;

        public VotingAndMiningTests()
        {
            this.testPubKey = new Mnemonic("lava frown leave virtual wedding ghost sibling able liar wide wisdom mammal").DeriveExtKey().PrivateKey.PubKey;
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
        // Checks that node can sync from scratch if federation voted in favor of kicking a fed member.
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

        [Fact]
        public async Task CanAddAndRemoveSameFedMemberAsync()
        {
            int originalFedMembersCount = this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count;

            TestHelper.Connect(this.node1, this.node2);
            TestHelper.Connect(this.node2, this.node3);

            await this.AllVoteAndMineAsync(this.testPubKey, true);

            Assert.Equal(originalFedMembersCount + 1, this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);

            await this.AllVoteAndMineAsync(this.testPubKey, false);

            Assert.Equal(originalFedMembersCount, this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);

            await this.AllVoteAndMineAsync(this.testPubKey, true);

            Assert.Equal(originalFedMembersCount + 1, this.node1.FullNode.NodeService<FederationManager>().GetFederationMembers().Count);
        }

        [Fact]
        public async Task ReorgRevertsAppliedChangesAsync()
        {
            TestHelper.Connect(this.node1, this.node2);

            var model = new HexPubKeyModel() { PubKeyHex = this.testPubKey.ToHex() };

            this.node1.FullNode.NodeService<VotingController>().VoteAddFedMember(model);
            this.node1.FullNode.NodeService<VotingController>().VoteKickFedMember(model);
            await this.node1.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            this.node2.FullNode.NodeService<VotingController>().VoteAddFedMember(model);
            await this.node2.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Single(this.node2.FullNode.NodeService<VotingManager>().GetPendingPolls());
            Assert.Single(this.node2.FullNode.NodeService<VotingManager>().GetFinishedPolls());

            await this.node3.MineBlocksAsync(4);
            TestHelper.Connect(this.node2, this.node3);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2, this.node3);

            Assert.Empty(this.node2.FullNode.NodeService<VotingManager>().GetPendingPolls());
            Assert.Empty(this.node2.FullNode.NodeService<VotingManager>().GetFinishedPolls());
        }

        private async Task AllVoteAndMineAsync(PubKey key, bool add)
        {
            await this.VoteAndMineBlockAsync(key, add, this.node1);
            await this.VoteAndMineBlockAsync(key, add, this.node2);
            await this.VoteAndMineBlockAsync(key, add, this.node3);

            await this.node1.MineBlocksAsync((int) this.network.Consensus.MaxReorgLength + 1);
        }

        private async Task VoteAndMineBlockAsync(PubKey key, bool add, CoreNode node)
        {
            var model = new HexPubKeyModel() { PubKeyHex = key.ToHex() };

            if (add)
                node.FullNode.NodeService<VotingController>().VoteAddFedMember(model);
            else
                node.FullNode.NodeService<VotingController>().VoteKickFedMember(model);

            await node.MineBlocksAsync(1);

            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2, this.node3);
        }

        [Fact]
        public async Task CanVoteToWhitelistAndRemoveHashesAsync()
        {
            int maxReorg = (int) this.network.Consensus.MaxReorgLength;

            Assert.Empty(this.node1.FullNode.NodeService<IWhitelistedHashesRepository>().GetHashes());
            TestHelper.Connect(this.node1, this.node2);

            await this.node1.MineBlocksAsync(1);

            var model = new HashModel() { Hash = Hashes.Hash256(RandomUtils.GetUInt64().ToBytes()).ToString()};

            // Node 1 votes to add hash
            this.node1.FullNode.NodeService<VotingController>().VoteWhitelistHash(model);
            await this.node1.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            // Node 2 votes to add hash
            this.node2.FullNode.NodeService<VotingController>().VoteWhitelistHash(model);
            await this.node2.MineBlocksAsync(maxReorg + 2);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Single(this.node1.FullNode.NodeService<IWhitelistedHashesRepository>().GetHashes());

            // Node 1 votes to remove hash
            this.node1.FullNode.NodeService<VotingController>().VoteRemoveHash(model);
            await this.node1.MineBlocksAsync(1);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            // Node 2 votes to remove hash
            this.node2.FullNode.NodeService<VotingController>().VoteRemoveHash(model);
            await this.node2.MineBlocksAsync(maxReorg + 2);
            CoreNodePoAExtensions.WaitTillSynced(this.node1, this.node2);

            Assert.Empty(this.node1.FullNode.NodeService<IWhitelistedHashesRepository>().GetHashes());
        }

        [Fact]
        public void NodeCanLoadFederationKey()
        {
            var network = new TestPoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                // Create first node as fed member.
                Key key = network.FederationKey1;
                CoreNode node = builder.CreatePoANode(network, key).Start();

                Assert.True(node.FullNode.NodeService<FederationManager>().IsFederationMember);
                Assert.Equal(node.FullNode.NodeService<FederationManager>().FederationMemberKey, key);

                // Create second node as normal node.
                CoreNode node2 = builder.CreatePoANode(network).Start();

                Assert.False(node2.FullNode.NodeService<FederationManager>().IsFederationMember);
                Assert.Equal(node2.FullNode.NodeService<FederationManager>().FederationMemberKey, null);
            }
        }

        [Fact]
        public async Task NodeCanMineAsync()
        {
            var network = new TestPoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                CoreNode node = builder.CreatePoANode(network, network.FederationKey1).Start();

                int tipBefore = node.GetTip().Height;

                await node.MineBlocksAsync(5).ConfigureAwait(false);

                Assert.True(node.GetTip().Height >= tipBefore + 5);
            }
        }

        [Fact]
        public async Task PremineIsReceivedAsync()
        {
            TestPoANetwork network = new TestPoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                string walletName = "mywallet";
                CoreNode node = builder.CreatePoANode(network, network.FederationKey1).WithWallet("pass", walletName).Start();

                IWalletManager walletManager = node.FullNode.NodeService<IWalletManager>();
                long balanceOnStart = walletManager.GetBalances(walletName, "account 0").Sum(x => x.AmountConfirmed);
                Assert.Equal(0, balanceOnStart);

                long toMineCount = network.Consensus.PremineHeight + network.Consensus.CoinbaseMaturity + 1 - node.GetTip().Height;

                await node.MineBlocksAsync((int)toMineCount).ConfigureAwait(false);

                long balanceAfterPremine = walletManager.GetBalances(walletName, "account 0").Sum(x => x.AmountConfirmed);

                Assert.Equal(network.Consensus.PremineReward.Satoshi, balanceAfterPremine);
            }
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }
}
