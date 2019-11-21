using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;
using Stratis.SmartContracts.CLR;

namespace Stratis.SmartContracts.IntegrationTests.RPC
{
    public class SmartContractRPCTests
    {
        private readonly Network network;
        private readonly SmartContractNodeBuilder builder;
        private readonly Func<int, CoreNode> nodeFactory;
        private readonly MethodParameterStringSerializer methodParameterStringSerializer;

        public SmartContractRPCTests()
        {
            var network = new SmartContractsPoARegTest();
            this.network = network;
            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodeFactory = (nodeIndex) => this.builder.CreateSmartContractPoANode(network, nodeIndex).Start();
            this.methodParameterStringSerializer = new MethodParameterStringSerializer(this.network);
        }

        [Fact]
        public async Task RPC_GetReceipt_Returns_Value()
        {
            using (var chain = new PoAMockChain(2, this.nodeFactory).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];

                // Get premine
                this.SetupNodes(chain, node1, node2);

                // Create a valid transaction.
                byte[] toSend = ContractCompiler.CompileFile("SmartContracts/StandardToken.cs").Compilation;

                var createParams = new[] {this.methodParameterStringSerializer.Serialize(10000uL)};
                BuildCreateContractTransactionResponse createResponse = node1.SendCreateContractTransaction(toSend, 0, createParams);
                node2.WaitMempoolCount(1);

                chain.MineBlocks(1);

                // Check for the receipt.
                RPCClient rpc = node2.CoreNode.CreateRPCClient();
                var result = await rpc.SendCommandAsync("getreceipt", createResponse.TransactionId.ToString());
                 
                Assert.True(result.Result.Value<bool>("success"));

                // Send a token.
                var parameters = new string[]
                {
                    this.methodParameterStringSerializer.Serialize(node1.MinerAddress.Address.ToAddress(node1.CoreNode.FullNode.Network)),
                    this.methodParameterStringSerializer.Serialize(1uL)
                };

                BuildCallContractTransactionResponse callResponse = node1.SendCallContractTransaction("TransferTo", createResponse.NewContractAddress, 0, parameters);

                node2.WaitMempoolCount(1);

                chain.MineBlocks(1);

                result = await rpc.SendCommandAsync("searchreceipts", createResponse.NewContractAddress, "TransferLog");

                Assert.True(result.Result.First.Value<bool>("success"));

                var logs = (JArray)result.Result.First["logs"];
                Assert.NotEmpty(logs);
            }
        }

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
