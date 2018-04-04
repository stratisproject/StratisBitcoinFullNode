using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification
    {
        private SharedSteps sharedSteps;
        private Transaction shorterChainTransaction;
        private Money shortChainTransactionFee;
        private int selfishBlockHeight;
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private const string AccountZero = "account 0";
        private const string WalletZero = "wallet 0";
        private const string WalletPassword = "123456";
        private const string SelfishSimon = "Selfish";
        private const string Bob = "B";
        private const string Charlie = "C";
        private const string Dave = "D";

        public ReorgToLongestChainSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder.Dispose();
        }

        private void four_miners_including_selfish_simon()
        {
            this.nodes = this.nodeGroupBuilder
                .StratisPowNode(SelfishSimon).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(Bob).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(Charlie).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(Dave).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .WithConnections()
                    .Connect(SelfishSimon, Bob)
                    .Connect(Bob, Charlie)
                    .Connect(Charlie, Dave)
                    .AndNoMoreConnections()
                .Build();
        }

        private void each_mine_a_block()
        {
            this.sharedSteps.MineBlocks(1, this.nodes[SelfishSimon], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.nodes[Bob], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.nodes[Charlie], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.nodes[Dave], AccountZero, WalletZero, WalletPassword);
        }

        private void selfish_simon_disconnects_and_mines_10_blocks()
        {
            this.nodes[SelfishSimon].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Bob].Endpoint);
            this.nodes[SelfishSimon].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Charlie].Endpoint);
            this.nodes[SelfishSimon].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Dave].Endpoint);

            TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(this.nodes[SelfishSimon]));

            this.sharedSteps.MineBlocks(10, this.nodes[SelfishSimon], AccountZero, WalletZero, WalletPassword);

            this.selfishBlockHeight = this.nodes[SelfishSimon].FullNode.Chain.Height;
        }

        private void bob_creates_a_transaction_and_broadcasts()
        {
            var nodeCReceivingAddress = this.GetSecondUnusedAddressToAvoidClashWithMiningAddress(this.nodes[Charlie]);

            var transactionBuildContext = SharedSteps.CreateTransactionBuildContext(WalletZero, AccountZero, WalletPassword, nodeCReceivingAddress.ScriptPubKey, Money.COIN * 1, FeeType.Medium, minConfirmations: 1);

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
            this.sharedSteps.MineBlocks(1, this.nodes[Charlie], AccountZero, WalletZero, WalletPassword, this.shortChainTransactionFee.Satoshi);
            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[Bob], this.nodes[Charlie], this.nodes[Dave]);
        }

        private void dave_confirms_it_ensures_tx_present()
        {
            var transaction = this.nodes[Dave].FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.shorterChainTransaction.GetHash()).Result;
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.shorterChainTransaction.GetHash());
        }

        private void selfish_simon_reconnects_and_broadcasts_his_longer_chain()
        {
            this.nodes[SelfishSimon].CreateRPCClient().AddNode(this.nodes[Bob].Endpoint);
            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[SelfishSimon], this.nodes[Bob], this.nodes[Charlie], this.nodes[Dave]);
        }
         
        private void bob_charlie_and_dave_reorg_to_selfish_simons_longest_chain()
        {
            TestHelper.WaitLoop(() => this.nodes[Bob].FullNode.Chain.Height == this.selfishBlockHeight);
            this.nodes[Bob].FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);
            TestHelper.WaitLoop(() => this.nodes[Charlie].FullNode.Chain.Height == this.selfishBlockHeight);
            this.nodes[Charlie].FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);
            TestHelper.WaitLoop(() => this.nodes[Dave].FullNode.Chain.Height == this.selfishBlockHeight);
            this.nodes[Dave].FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);
        }

        private void bobs_transaction_from_shorter_chain_is_now_missing()
        {
            this.nodes[Bob].FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.shorterChainTransaction.GetHash()).Result
                .Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes.");
        }

        private void bobs_transaction_is_not_returned_to_the_mem_pool()
        {
            this.nodes[Dave].CreateRPCClient().GetRawMempool()
                .Should().NotContain(x => x == this.shorterChainTransaction.GetHash(), "it is not implemented yet.");
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            var coinbaseMaturity = (int)this.nodes[Bob].FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.sharedSteps.MineBlocks(coinbaseMaturity, this.nodes[Bob], AccountZero, WalletZero, WalletPassword);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[SelfishSimon]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Bob]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Charlie]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Dave]));
        }
    }
}
