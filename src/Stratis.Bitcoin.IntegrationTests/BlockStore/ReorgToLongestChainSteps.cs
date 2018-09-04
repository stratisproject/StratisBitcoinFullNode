using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification
    {
        private Transaction shorterChainTransaction;
        private Money shortChainTransactionFee;
        private int jingsBlockHeight;
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private const string AccountZero = "account 0";
        private const string WalletZero = "wallet 0";
        private const string WalletPassword = "123456";
        private const string WalletPassphrase = "phrase";
        private const string JingTheFastMiner = "Jing";
        private const string Bob = "Bob";
        private const string Charlie = "Charlie";
        private const string Dave = "Dave";

        public ReorgToLongestChainSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName), KnownNetworks.RegTest);
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder.Dispose();
        }

        private void four_miners()
        {
            this.nodes = this.nodeGroupBuilder
                .StratisPowNode(JingTheFastMiner).Start().NotInIBD().WithWallet(WalletZero, WalletPassword, WalletPassphrase)
                .StratisPowNode(Bob).Start().NotInIBD().WithWallet(WalletZero, WalletPassword, WalletPassphrase)
                .StratisPowNode(Charlie).Start().NotInIBD().WithWallet(WalletZero, WalletPassword, WalletPassphrase)
                .StratisPowNode(Dave).Start().NotInIBD().WithWallet(WalletZero, WalletPassword, WalletPassphrase)
                .WithConnections()
                    .Connect(JingTheFastMiner, Bob)
                    .Connect(Bob, Charlie)
                    .Connect(Charlie, Dave)
                    .AndNoMoreConnections()
                .Build();
        }

        private void each_mine_a_block() => 
            this.nodes.Values.Select(node => TestHelper.MineBlocks(node, WalletZero, WalletPassword, AccountZero, 1));

        private void jing_loses_connection_to_others_but_carries_on_mining()
        {
            this.nodes[JingTheFastMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Bob].Endpoint);
            this.nodes[JingTheFastMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Charlie].Endpoint);
            this.nodes[JingTheFastMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Dave].Endpoint);

            TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(this.nodes[JingTheFastMiner]));

            TestHelper.MineBlocks(this.nodes[JingTheFastMiner], WalletZero, WalletPassword, AccountZero, 1);

            this.jingsBlockHeight = this.nodes[JingTheFastMiner].FullNode.Chain.Height;
        }

        private void bob_creates_a_transaction_and_broadcasts()
        {
            HdAddress nodeCReceivingAddress = this.GetSecondUnusedAddressToAvoidClashWithMiningAddress(this.nodes[Charlie]);

            TransactionBuildContext transactionBuildContext = TestHelper.CreateTransactionBuildContext(
                this.nodes[Bob].FullNode.Network,
                WalletZero,
                AccountZero,
                WalletPassword,
                new[] {
                    new Recipient {
                        Amount = Money.COIN * 1,
                        ScriptPubKey = nodeCReceivingAddress.ScriptPubKey
                    }
                },
                FeeType.Medium
                , 1);

            this.shorterChainTransaction = this.nodes[Bob].FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
            this.shortChainTransactionFee = this.nodes[Bob].FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);

            this.nodes[Bob].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.shorterChainTransaction.ToHex()));
        }

        private HdAddress GetSecondUnusedAddressToAvoidClashWithMiningAddress(CoreNode node)
        {
            return node.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletZero, AccountZero), 2)
                .Skip(1).First();
        }

        private void charlie_mines_this_block()
        {
            TestHelper.MineBlocks(this.nodes[Charlie], WalletZero, WalletPassword, AccountZero, 1);
            TestHelper.WaitForNodeToSync(this.nodes[Bob], this.nodes[Charlie], this.nodes[Dave]);
        }

        private void dave_confirms_transaction_is_present()
        {
            Transaction transaction = this.nodes[Dave].FullNode.BlockStore().GetTrxAsync(this.shorterChainTransaction.GetHash()).Result;
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.shorterChainTransaction.GetHash());
        }

        private void jings_connection_comes_back()
        {
            this.nodes[JingTheFastMiner].CreateRPCClient().AddNode(this.nodes[Bob].Endpoint);
            TestHelper.WaitForNodeToSync(this.nodes.Values.ToArray());
        }

        private void bob_charlie_and_dave_reorg_to_jings_longest_chain()
        {
            TestHelper.WaitLoop(() => this.nodes[Bob].FullNode.Chain.Height == this.jingsBlockHeight);
            TestHelper.WaitLoop(() => this.nodes[Charlie].FullNode.Chain.Height == this.jingsBlockHeight);
            TestHelper.WaitLoop(() => this.nodes[Dave].FullNode.Chain.Height == this.jingsBlockHeight);
        }

        private void bobs_transaction_from_shorter_chain_is_now_missing()
        {
            this.nodes[Bob].FullNode.BlockStore().GetTrxAsync(this.shorterChainTransaction.GetHash()).Result
                .Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes");
        }

        private void bobs_transaction_is_now_in_the_mem_pool()
        {
            this.nodes[Dave].CreateRPCClient().GetRawMempool()
                .Should().Contain(x => x == this.shorterChainTransaction.GetHash(), "transaction should be in the mempool when not mined in a longer chain");
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            var coinbaseMaturity = (uint)this.nodes[Bob].FullNode.Network.Consensus.CoinbaseMaturity;

            TestHelper.MineBlocks(this.nodes[Bob], WalletZero, WalletPassword, AccountZero, coinbaseMaturity);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[JingTheFastMiner]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Bob]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Charlie]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Dave]));

            // Ensure that all the nodes are synced to at least coinbase maturity.
            TestHelper.WaitLoop(() => this.nodes[JingTheFastMiner].FullNode.ConsensusManager().Tip.Height >= this.nodes[Charlie].FullNode.Network.Consensus.CoinbaseMaturity);
            TestHelper.WaitLoop(() => this.nodes[Bob].FullNode.ConsensusManager().Tip.Height >= this.nodes[Charlie].FullNode.Network.Consensus.CoinbaseMaturity);
            TestHelper.WaitLoop(() => this.nodes[Charlie].FullNode.ConsensusManager().Tip.Height >= this.nodes[Charlie].FullNode.Network.Consensus.CoinbaseMaturity);
            TestHelper.WaitLoop(() => this.nodes[Dave].FullNode.ConsensusManager().Tip.Height >= this.nodes[Charlie].FullNode.Network.Consensus.CoinbaseMaturity);
        }

        private void meanwhile_jings_chain_advanced_ahead_of_the_others()
        {
            TestHelper.MineBlocks(this.nodes[JingTheFastMiner], WalletZero, WalletPassword, AccountZero, 5);

            this.jingsBlockHeight = this.nodes[JingTheFastMiner].FullNode.Chain.Height;
        }
    }
}
