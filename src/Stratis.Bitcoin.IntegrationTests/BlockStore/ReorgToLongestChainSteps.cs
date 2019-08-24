﻿using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification
    {
        private NodeBuilder nodeBuilder;
        private Network network;
        private CoreNode jingNode;
        private CoreNode bobNode;
        private CoreNode charlieNode;
        private CoreNode daveNode;
        private Transaction shorterChainTransaction;
        private int jingsBlockHeight;

        private const string AccountZero = "account 0";
        private const string WalletZero = "mywallet";
        private const string WalletPassword = "password";

        public ReorgToLongestChainSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.network = new BitcoinRegTest();
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void four_miners()
        {
            this.bobNode = this.nodeBuilder.CreateStratisPowNode(this.network).AlwaysFlushBlocks().WithWallet().Start();
            this.charlieNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();
            this.daveNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();
            this.jingNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();

            TestHelper.Connect(this.jingNode, this.bobNode);
            TestHelper.Connect(this.jingNode, this.charlieNode);
            TestHelper.Connect(this.jingNode, this.daveNode);

            TestHelper.Connect(this.bobNode, this.charlieNode);
            TestHelper.Connect(this.charlieNode, this.daveNode);
        }

        private void each_mine_a_block()
        {
            TestHelper.MineBlocks(this.bobNode, 1);
            TestHelper.MineBlocks(this.charlieNode, 1);
            TestHelper.MineBlocks(this.daveNode, 1);
            TestHelper.MineBlocks(this.jingNode, 1);
        }

        private void jing_loses_connection_to_others_but_carries_on_mining()
        {
            TestHelper.Disconnect(this.jingNode, this.bobNode);
            TestHelper.Disconnect(this.jingNode, this.charlieNode);
            TestHelper.Disconnect(this.jingNode, this.daveNode);

            TestBase.WaitLoop(() => !TestHelper.IsNodeConnected(this.jingNode));

            TestHelper.MineBlocks(this.jingNode, 1);

            this.jingsBlockHeight = this.jingNode.FullNode.ChainIndexer.Height;
        }

        private void bob_creates_a_transaction_and_broadcasts()
        {
            HdAddress charlieAddress = this.GetSecondUnusedAddressToAvoidClashWithMiningAddress(this.charlieNode);

            TransactionBuildContext transactionBuildContext = TestHelper.CreateTransactionBuildContext(
                this.bobNode.FullNode.Network,
                WalletZero,
                AccountZero,
                WalletPassword,
                new[] {
                    new Recipient {
                        Amount = Money.COIN * 1,
                        ScriptPubKey = charlieAddress.ScriptPubKey
                    }
                },
                FeeType.Medium
                , 1);

            this.shorterChainTransaction = this.bobNode.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
            Money shortChainTransactionFee = this.bobNode.FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);

            this.bobNode.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(this.shorterChainTransaction.ToHex()));
        }

        private HdAddress GetSecondUnusedAddressToAvoidClashWithMiningAddress(CoreNode node)
        {
            return node.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletZero, AccountZero), 2)
                .Skip(1).First();
        }

        private void charlie_waits_for_the_trx_and_mines_this_block()
        {
            TestBase.WaitLoop(() => this.charlieNode.FullNode.MempoolManager().GetTransaction(this.shorterChainTransaction.GetHash()).Result != null);

            TestHelper.MineBlocks(this.charlieNode, 1);
            TestHelper.WaitForNodeToSync(this.bobNode, this.charlieNode, this.daveNode);
        }

        private void dave_confirms_transaction_is_present()
        {
            Transaction transaction = this.daveNode.FullNode.BlockStore().GetTransactionById(this.shorterChainTransaction.GetHash());
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.shorterChainTransaction.GetHash());
        }

        private void meanwhile_jings_chain_advanced_ahead_of_the_others()
        {
            TestHelper.MineBlocks(this.jingNode, 5);

            this.jingsBlockHeight = this.jingNode.FullNode.ChainIndexer.Height;
        }

        private void jings_connection_comes_back()
        {
            TestHelper.Connect(this.jingNode, this.bobNode);
            TestHelper.WaitForNodeToSyncIgnoreMempool(this.jingNode, this.bobNode, this.charlieNode, this.daveNode);
        }

        private void bob_charlie_and_dave_reorg_to_jings_longest_chain()
        {
            TestBase.WaitLoop(() => this.bobNode.FullNode.ChainIndexer.Height == this.jingsBlockHeight);
            TestBase.WaitLoop(() => this.charlieNode.FullNode.ChainIndexer.Height == this.jingsBlockHeight);
            TestBase.WaitLoop(() => this.daveNode.FullNode.ChainIndexer.Height == this.jingsBlockHeight);
        }

        private void bobs_transaction_from_shorter_chain_is_now_missing()
        {
            TestBase.WaitLoop(() => this.bobNode.FullNode.BlockStore().GetTransactionById(this.shorterChainTransaction.GetHash()) == null, waitTimeSeconds: 300);
            this.bobNode.FullNode.BlockStore().GetTransactionById(this.shorterChainTransaction.GetHash())
                .Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes");
        }

        private void bobs_transaction_is_now_in_the_mem_pool()
        {
            this.daveNode.CreateRPCClient().GetRawMempool()
                .Should().Contain(x => x == this.shorterChainTransaction.GetHash(), "transaction should be in the mempool when not mined in a longer chain");
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            int coinbaseMaturity = (int)this.bobNode.FullNode.Network.Consensus.CoinbaseMaturity;

            TestHelper.MineBlocks(this.bobNode, coinbaseMaturity + 1);

            TestBase.WaitLoop(() => TestHelper.IsNodeSynced(this.bobNode));
            TestBase.WaitLoop(() => TestHelper.IsNodeSynced(this.charlieNode));
            TestBase.WaitLoop(() => TestHelper.IsNodeSynced(this.daveNode));
            TestBase.WaitLoop(() => TestHelper.IsNodeSynced(this.jingNode));

            // Ensure that all the nodes are synced to at least coinbase maturity.
            TestBase.WaitLoop(() => this.bobNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
            TestBase.WaitLoopMessage(() => (this.charlieNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity, $"CHARLIENODE_TIP_{this.charlieNode.FullNode.ConsensusManager().Tip}"));
            TestBase.WaitLoop(() => this.daveNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
            TestBase.WaitLoop(() => this.jingNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
        }
    }
}
