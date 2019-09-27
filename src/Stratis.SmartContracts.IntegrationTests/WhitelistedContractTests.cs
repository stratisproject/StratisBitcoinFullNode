using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class WhitelistedContractTests : IDisposable
    {
        private readonly SmartContractsPoAWhitelistRegTest network;
        private readonly Func<int, CoreNode> nodeFactory;
        private readonly SmartContractNodeBuilder builder;

        public WhitelistedContractTests()
        {
            this.network = new SmartContractsPoAWhitelistRegTest();

            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodeFactory = (nodeIndex) => this.builder.CreateWhitelistedContractPoANode(this.network, nodeIndex).Start();
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }

        [Retry]
        public void Create_Whitelisted_Contract()
        {
            using (var chain = new PoAMockChain(2, this.nodeFactory).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Compile file
                byte[] toSend = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs").Compilation;

                // Add the hash to all the nodes on the chain.
                chain.WhitelistCode(toSend);

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(toSend, 30);
                node1.WaitMempoolCount(1);
                chain.MineBlocks(1);

                // Check the balance exists at contract location.
                Assert.Equal((ulong)30 * 100_000_000, node1.GetContractBalance(sendResponse.NewContractAddress));
            }
        }

        // TODO: Fix this.
        /*
        [Retry]
        public async Task Create_NoWhitelist_Mempool_RejectsAsync()
        {
            using (var chain = new PoAMockChain(2, this.nodeFactory).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Create a valid transaction.
                byte[] toSend = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs").Compilation;
                var buildResult = node1.BuildCreateContractTransaction(toSend, 0);

                Transaction tx = node1.CoreNode.FullNode.Network.CreateTransaction(buildResult.Hex);

                var broadcasterManager = node1.CoreNode.FullNode.NodeService<IBroadcasterManager>();

                await broadcasterManager.BroadcastTransactionAsync(tx);

                // Give it enough time to reach if it was valid.
                Thread.Sleep(3000);

                // Nothing arrives.
                Assert.Empty(node1.CoreNode.CreateRPCClient().GetRawMempool());

                // If we were to whitelist it later, the mempool increases.
                chain.WhitelistCode(toSend);

                await broadcasterManager.BroadcastTransactionAsync(tx);
                node1.WaitMempoolCount(1);
            }
        }
        */

        private void SetupNodes(IMockChain chain, MockChainNode node1, MockChainNode node2)
        {
            // TODO: Use ready chain data
            // Get premine
            chain.MineBlocks(10);

            // Send half to other from whoever received premine
            if ((long)node1.WalletSpendableBalance == node1.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi)
            {
                PayHalfPremine(chain, node1, node2);
            }
            else
            {
                PayHalfPremine(chain, node2, node1);
            }
        }

        private void PayHalfPremine(IMockChain chain, MockChainNode from, MockChainNode to)
        {
            from.SendTransaction(to.MinerAddress.ScriptPubKey, new Money(from.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            from.WaitMempoolCount(1);
            chain.MineBlocks(1);
        }
    }
}