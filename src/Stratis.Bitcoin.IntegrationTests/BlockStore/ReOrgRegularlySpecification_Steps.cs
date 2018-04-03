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
    public partial class ReOrgRegularlySpecification
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
        private const string SelfishMiner = "Selfish";
        private const string NodeB = "B";
        private const string NodeC = "C";
        private const string NodeD = "D";

        public ReOrgRegularlySpecification(ITestOutputHelper output) : base(output)
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

        private void four_nodes()
        {
            this.nodes = this.nodeGroupBuilder
                .StratisPowNode(SelfishMiner).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(NodeB).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(NodeC).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(NodeD).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .WithConnections()
                    .Connect(SelfishMiner, NodeB)
                    .Connect(NodeB, NodeC)
                    .Connect(NodeC, NodeD)
                    .AndNoMoreConnections()
                .Build();
        }

        private void each_mine_a_block()
        {
            this.sharedSteps.MineBlocks(1, this.nodes[SelfishMiner], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.nodes[NodeB], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.nodes[NodeC], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.nodes[NodeD], AccountZero, WalletZero, WalletPassword);
        }

        private void selfish_miner_disconnects_and_mines_10_blocks()
        {
            this.nodes[SelfishMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[NodeB].Endpoint);
            this.nodes[SelfishMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[NodeC].Endpoint);
            this.nodes[SelfishMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[NodeD].Endpoint);

            TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(this.nodes[SelfishMiner]));

            this.sharedSteps.MineBlocks(10, this.nodes[SelfishMiner], AccountZero, WalletZero, WalletPassword);

            this.selfishBlockHeight = this.nodes[SelfishMiner].FullNode.Chain.Height;
        }

        private void nodeB_creates_a_transaction_and_broadcasts()
        {
            var nodeCReceivingAddress = this.GetSecondUnusedAddressToAvoidClashWithMiningAddress(this.nodes[NodeC]);

            var transactionBuildContext = SharedSteps.CreateTransactionBuildContext(WalletZero, AccountZero, WalletPassword, nodeCReceivingAddress.ScriptPubKey, Money.COIN * 1, FeeType.Medium, minConfirmations: 1);

            this.shorterChainTransaction = this.nodes[NodeB].FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
            this.shortChainTransactionFee = this.nodes[NodeB].FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);

            this.nodes[NodeB].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.shorterChainTransaction.ToHex()));
        }

        private HdAddress GetSecondUnusedAddressToAvoidClashWithMiningAddress(CoreNode node)
        {
            return node.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletZero, AccountZero), 2)
                .Skip(1).First();
        }

        private void nodeC_mines_this_block()
        {
            this.sharedSteps.MineBlocks(1, this.nodes[NodeC], AccountZero, WalletZero, WalletPassword, this.shortChainTransactionFee.Satoshi);
        }

        private void nodeD_confirms_it_ensures_tx_present()
        {
            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeB], this.nodes[NodeC], this.nodes[NodeD]);

            var transaction = this.nodes[NodeD].FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.shorterChainTransaction.GetHash()).Result;
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.shorterChainTransaction.GetHash());
        }

        private void selfish_node_reconnects_and_broadcasts()
        {
            this.nodes[SelfishMiner].CreateRPCClient().AddNode(this.nodes[NodeB].Endpoint);
            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[SelfishMiner], this.nodes[NodeB], this.nodes[NodeC], this.nodes[NodeD]);
        }
         
        private void other_nodes_reorg_to_longest_chain()
        {
            TestHelper.WaitLoop(() => this.nodes[NodeB].FullNode.Chain.Height == this.selfishBlockHeight);
            this.nodes[NodeB].FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);
            TestHelper.WaitLoop(() => this.nodes[NodeC].FullNode.Chain.Height == this.selfishBlockHeight);
            this.nodes[NodeC].FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);
            TestHelper.WaitLoop(() => this.nodes[NodeD].FullNode.Chain.Height == this.selfishBlockHeight);
            this.nodes[NodeD].FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);
        }

        private void transaction_from_shorter_chain_is_missing()
        {
            this.nodes[NodeB].FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.shorterChainTransaction.GetHash()).Result
                .Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes.");
        }

        private void transaction_is_not_returned_to_the_mem_pool()
        {
            this.nodes[NodeD].CreateRPCClient().GetRawMempool()
                .Should().NotContain(x => x == this.shorterChainTransaction.GetHash(), "it is not implemented yet.");
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            var coinbaseMaturity = (int)this.nodes[NodeB].FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.sharedSteps.MineBlocks(coinbaseMaturity, this.nodes[NodeB], AccountZero, WalletZero, WalletPassword);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[SelfishMiner]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[NodeB]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[NodeC]));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[NodeD]));
        }
    }
}
