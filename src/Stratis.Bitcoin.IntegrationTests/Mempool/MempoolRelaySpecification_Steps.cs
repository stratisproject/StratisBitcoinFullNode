using System.IO;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Common;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public partial class MempoolRelaySpecification
    {
        private NodeBuilder nodeBuilder;
        private CoreNode nodeA;
        private CoreNode nodeB;
        private CoreNode nodeC;
        private int coinbaseMaturity;
        private Transaction transaction;

        // NOTE: This constructor is allows test steps names to be logged
        public MempoolRelaySpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        protected void nodeA_nodeB_and_nodeC()
        {
            Network regTest = KnownNetworks.RegTest;

            this.nodeA = this.nodeBuilder.CreateStratisPowNode(regTest);
            this.nodeB = this.nodeBuilder.CreateStratisPowNode(regTest);
            this.nodeC = this.nodeBuilder.CreateStratisPowNode(regTest);

            this.nodeBuilder.StartAll();
            this.nodeA.NotInIBD().WithWallet();
            this.nodeB.NotInIBD().WithWallet();
            this.nodeC.NotInIBD().WithWallet();

            this.coinbaseMaturity = (int)this.nodeA.FullNode.Network.Consensus.CoinbaseMaturity;
        }

        protected void nodeA_mines_coins_that_are_spendable()
        {
            // add some coins to nodeA
            this.nodeA.SetDummyMinerSecret(new BitcoinSecret(new Key(), this.nodeA.FullNode.Network));
            this.nodeA.GenerateStratisWithMiner(this.coinbaseMaturity + 1);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodeA));
        }

        protected void nodeA_connects_to_nodeB()
        {
            this.nodeA.CreateRPCClient().AddNode(this.nodeB.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.nodeA, this.nodeB));
        }

        protected void nodeA_nodeB_and_nodeC_are_NON_whitelisted()
        {
            this.nodeA.FullNode.NodeService<IConnectionManager>().ConnectedPeers.First().Behavior<ConnectionManagerBehavior>().Whitelisted = false;
            this.nodeB.FullNode.NodeService<IConnectionManager>().ConnectedPeers.First().Behavior<ConnectionManagerBehavior>().Whitelisted = false;
            this.nodeC.FullNode.NodeService<IConnectionManager>().ConnectedPeers.First().Behavior<ConnectionManagerBehavior>().Whitelisted = false;
        }

        protected void nodeB_connects_to_nodeC()
        {
            this.nodeB.CreateRPCClient().AddNode(this.nodeC.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.nodeB, this.nodeC));
        }

        protected void nodeA_creates_a_transaction_and_propagates_to_nodeB()
        {
            Block block = this.nodeA.FullNode.BlockStore().GetBlockAsync(this.nodeA.FullNode.Chain.GetBlock(1).HashBlock).Result;
            Transaction prevTrx = block.Transactions.First();
            var dest = new BitcoinSecret(new Key(), this.nodeA.FullNode.Network);

            this.transaction = this.nodeA.FullNode.Network.CreateTransaction();
            this.transaction.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(this.nodeA.MinerSecret.PubKey)));
            this.transaction.AddOutput(new TxOut("25", dest.PubKey.Hash));
            this.transaction.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
            this.transaction.Sign(this.nodeA.FullNode.Network, this.nodeA.MinerSecret, new Coin(this.transaction.Inputs.First().PrevOut, prevTrx.Outputs.First()));

            this.nodeA.Broadcast(this.transaction);
        }

        protected void the_transaction_is_propagated_to_nodeC()
        {
            RPCClient rpc = this.nodeC.CreateRPCClient();
            TestHelper.WaitLoop(() => rpc.GetRawMempool().Any());

            rpc.GetRawMempool()
                .Should().ContainSingle()
                .Which.IsSameOrEqualTo(this.transaction.GetHash());
        }
    }
}