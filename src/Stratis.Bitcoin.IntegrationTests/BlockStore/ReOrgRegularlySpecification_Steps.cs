using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

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

            this.selfishMiner.CreateRPCClient().AddNode(this.secondNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.secondNode, this.selfishMiner));

            this.secondNode.CreateRPCClient().AddNode(this.thirdNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.thirdNode, this.secondNode));

            this.thirdNode.CreateRPCClient().AddNode(this.fourthNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.fourthNode, this.thirdNode));
        }

        private void each_mine_10_blocks()
        {
            this.sharedSteps.MineBlocks(10, this.selfishMiner, accountName, walletName, walletPassword);
            this.sharedSteps.MineBlocks(10, this.secondNode, accountName, walletName, walletPassword);
            this.sharedSteps.MineBlocks(10, this.thirdNode, accountName, walletName, walletPassword);
            this.sharedSteps.MineBlocks(10, this.fourthNode, accountName, walletName, walletPassword);
        }

        private void first_node_disconnects_and_selfishly_mines_10_blocks()
        {
            throw new System.NotImplementedException();
        }

        private void second_node_creates_a_transaction_and_broadcasts()
        {
            throw new System.NotImplementedException();
        }

        private void third_node_mines_this_block()
        {
            throw new System.NotImplementedException();
        }

        private void fouth_node_confirms_it()
        {
            throw new System.NotImplementedException();
        }

        private void first_node_reconnects_and_broadcasts()
        {
            throw new System.NotImplementedException();
        }

        private void second_third_and_fourth_node_reorg_to_longest_chain()
        {
            throw new System.NotImplementedException();
        }

        private void transaction_is_returned_to_the_mem_pool()
        {
            throw new System.NotImplementedException();
        }

        private void transaction_from_shorter_chain_is_missing()
        {
            throw new System.NotImplementedException();
        }
    }
}
