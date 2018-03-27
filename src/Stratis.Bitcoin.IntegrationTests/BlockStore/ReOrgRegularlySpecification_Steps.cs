using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReOrgRegularlySpecification
    {
        private NodeBuilder nodeBuilder;
        private CoreNode selfishMiner;
        private CoreNode secondNode;
        private CoreNode thirdNode;
        private CoreNode fourthNode;
        private SharedSteps sharedSteps;
        private Transaction secondNodeTransaction;
        private int selfishBlockHeight;
        private const string AccountZero = "account 0";
        private const string WalletZero = "wallet 0";
        private const string WalletPassword = "123456";

        public ReOrgRegularlySpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
        }

        private void four_nodes()
        {
            this.nodeBuilder = NodeBuilder.Create();

            this.selfishMiner = this.nodeBuilder.CreateStratisPowNode();
            this.secondNode = this.nodeBuilder.CreateStratisPowNode();
            this.thirdNode = this.nodeBuilder.CreateStratisPowNode();
            this.fourthNode = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();

            this.selfishMiner.NotInIBD();
            this.secondNode.NotInIBD();
            this.thirdNode.NotInIBD();
            this.fourthNode.NotInIBD();

            this.selfishMiner.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);
            this.secondNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);
            this.thirdNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);
            this.fourthNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);

            this.selfishMiner.CreateRPCClient().AddNode(this.secondNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.secondNode, this.selfishMiner));

            this.secondNode.CreateRPCClient().AddNode(this.thirdNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.thirdNode, this.secondNode));

            this.thirdNode.CreateRPCClient().AddNode(this.fourthNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.fourthNode, this.thirdNode));
        }

        private void each_mine_a_blocks()
        {
            this.sharedSteps.MineBlocks(1, this.selfishMiner, AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.secondNode, AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.thirdNode, AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.fourthNode, AccountZero, WalletZero, WalletPassword);
        }

        private void selfish_miner_disconnects_and_mines_10_blocks()
        {
            this.selfishMiner.FullNode.ConnectionManager.RemoveNodeAddress(this.secondNode.Endpoint);
            this.selfishMiner.FullNode.ConnectionManager.RemoveNodeAddress(this.thirdNode.Endpoint);
            this.selfishMiner.FullNode.ConnectionManager.RemoveNodeAddress(this.fourthNode.Endpoint);
            TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(this.selfishMiner));

            this.sharedSteps.MineBlocks(10, this.selfishMiner, AccountZero, WalletZero, WalletPassword);

            this.selfishBlockHeight = this.selfishMiner.FullNode.Chain.Height;
        }

        private void second_node_creates_a_transaction_and_broadcasts()
        {
            var thirdNodeReceivingAddress = this.thirdNode.FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(WalletZero, AccountZero));

            this.secondNodeTransaction = this.secondNode.FullNode.WalletTransactionHandler().BuildTransaction(
                SharedSteps.CreateTransactionBuildContext(new WalletAccountReference(WalletZero, AccountZero), WalletPassword, thirdNodeReceivingAddress.ScriptPubKey,
                    Money.COIN * 1, FeeType.Medium, 101));

            this.secondNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.secondNodeTransaction.ToHex()));
        }

        private void third_node_mines_this_block()
        {
            int feeForSecondToThirdTransaction = 100004520; /////?????
            this.sharedSteps.MineBlocks(1, this.thirdNode, AccountZero, WalletZero, WalletPassword, feeForSecondToThirdTransaction);
        }

        private void fouth_node_confirms_it_ensures_tx_present()
        {
            this.sharedSteps.WaitForBlockStoreToSync(this.secondNode, this.thirdNode, this.fourthNode);

            var transaction = this.fourthNode.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.secondNodeTransaction.GetHash()).Result;
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.secondNodeTransaction.GetHash());
        }

        private void selfish_node_reconnects_and_broadcasts()
        {
            this.selfishMiner.CreateRPCClient().AddNode(this.secondNode.Endpoint);
            this.sharedSteps.WaitForBlockStoreToSync(this.selfishMiner, this.secondNode, this.thirdNode, this.fourthNode);
        }

        private void second_third_and_fourth_node_reorg_to_longest_chain()
        {
            TestHelper.WaitLoop(() => this.secondNode.FullNode.Chain.Height == this.selfishBlockHeight);
            this.secondNode.FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);

            TestHelper.WaitLoop(() => this.thirdNode.FullNode.Chain.Height == this.selfishBlockHeight);
            TestHelper.WaitLoop(() => this.fourthNode.FullNode.Chain.Height == this.selfishBlockHeight);
        }

        private void transaction_from_shorter_chain_is_missing()
        {
            var transaction = this.secondNode.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.secondNodeTransaction.GetHash()).Result;
            transaction.Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes.");
        }

        private void transaction_is_returned_to_the_mem_pool()
        {
            var transaction = this.fourthNode.FullNode.MempoolManager().GetTransaction(this.secondNodeTransaction.GetHash()).GetAwaiter().GetResult();
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.secondNodeTransaction.GetHash());
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            var coinbaseMaturity = (int)this.secondNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.sharedSteps.MineBlocks(coinbaseMaturity, this.secondNode, AccountZero, WalletZero, WalletPassword);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.selfishMiner));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.secondNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.thirdNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.fourthNode));
        }
    }
}
