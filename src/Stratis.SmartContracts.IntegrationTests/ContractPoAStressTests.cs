using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.Tests.Common;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractPoAStressTests : IClassFixture<PoAMockChainFixture3Nodes>
    {
        private readonly IMockChain mockChain;

        public ContractPoAStressTests(PoAMockChainFixture3Nodes fixture)
        {
            this.mockChain = fixture.Chain;
        }

        [Fact]
        public void ReorgedCoinbaseUtxoRemovedFromMempool()
        {
            var node1 = this.mockChain.Nodes[0];
            var node2 = this.mockChain.Nodes[1];
            var node3 = this.mockChain.Nodes[2];

            int startingHeight = node1.CoreNode.FullNode.ChainIndexer.Height;

            // Nodes are syncing together...
            this.mockChain.MineBlocks(1);

            // Node 1 loses connection to the others
            foreach (var peer in node1.CoreNode.FullNode.ConnectionManager.ConnectedPeers.ToList())
            {
                peer.Disconnect("For Testing");
            }

            TestBase.WaitLoop(() => !node1.CoreNode.FullNode.ConnectionManager.ConnectedPeers.Any());

            // Node 2 sends contract tx and creates its own chain with node 3
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = node2.SendCreateContractTransaction(compilationResult.Compilation, 0, gasLimit: SmartContractFormatLogic.GasLimitMaximum);
            node3.WaitMempoolCount(1);
            node3.CoreNode.MineBlocksAsync(2).GetAwaiter().GetResult();
            TestBase.WaitLoop(() => node2.CoreNode.FullNode.ChainIndexer.Height == startingHeight + 3);

            // Node gets a refund utxo in the coinbase
            var unspents = node2.SpendableTransactions.ToList();
            Assert.True(unspents[1].Transaction.IsCoinBase);

            // Refund utxo is used to build a new transaction
            BuildCreateContractTransactionResponse response2 = node2.SendCreateContractTransaction(compilationResult.Compilation, 0, gasLimit: 15000uL, outpoints: new List<OutpointRequest>
            {
                new OutpointRequest
                {
                    Index = unspents[1].Transaction.Index,
                    TransactionId = unspents[1].Transaction.Id.ToString()
                }
            });

            Transaction tx = node1.CoreNode.FullNode.Network.CreateTransaction(response2.Hex);
            Assert.Equal(unspents[1].Transaction.Id, tx.Inputs[0].PrevOut.Hash);
            node2.WaitMempoolCount(1);
            node3.WaitMempoolCount(1);

            // Other node mines far ahead
            node1.CoreNode.MineBlocksAsync(5).GetAwaiter().GetResult();
            Assert.True(node2.CoreNode.FullNode.ChainIndexer.Height == startingHeight + 3);
            Assert.True(node3.CoreNode.FullNode.ChainIndexer.Height == startingHeight + 3);
            Assert.True(node1.CoreNode.FullNode.ChainIndexer.Height == startingHeight + 6);

            // Reconnect nodes.
            TestHelper.Connect(node1.CoreNode, node2.CoreNode);
            TestHelper.Connect(node1.CoreNode, node3.CoreNode);

            // 2 and 3 will reorg to 1's chain.
            TestBase.WaitLoop(() => node2.CoreNode.FullNode.ChainIndexer.Height == startingHeight + 6);
            TestBase.WaitLoop(() => node3.CoreNode.FullNode.ChainIndexer.Height == startingHeight + 6);

            // Lets give some time to the nodes to try and sort themselves out.
            Thread.Sleep(5_000);

            // Tx funded by the refund should no longer be valid on 2 or 3. It shouldn't be in the mempool.
            List<TxMempoolInfo> node2MempoolInfo = node2.CoreNode.FullNode.MempoolManager().InfoAll().ToList();
            foreach (var mempoolInfo in node2MempoolInfo)
            {
                Assert.NotEqual(response2.TransactionId, mempoolInfo.Trx.GetHash());
            }
        }
    }
}
