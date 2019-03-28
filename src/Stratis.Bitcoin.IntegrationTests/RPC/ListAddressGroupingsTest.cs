using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Wallet;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public sealed class ListAddressGroupingsTest
    {
        [Fact]
        public async Task ListAddressGroupingsAsync()
        {
            var walletName = "mywallet";
            var password = "password";
            var accountName = "account 0";

            using (var builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinRegTest();

                var nodeConfig = new NodeConfigParameters
                {
                    { "-txIndex", "1" }
                };

                var nodeA = builder.CreateStratisPowNode(network, agent: "nodeA", configParameters: nodeConfig).WithWallet().Start();
                var nodeB = builder.CreateStratisPowNode(network, agent: "nodeB", configParameters: nodeConfig).WithWallet().Start();

                // Mine some blocks to get some coins.
                TestHelper.MineBlocks(nodeA, 101);

                // Sync nodeA with nodeB.
                TestHelper.ConnectAndSync(nodeA, nodeB);

                RPCClient rpcClient = nodeA.CreateRPCClient();
                RPCResponse response = await rpcClient.SendCommandAsync(RPCOperations.getbalance, "*");
                response.Result.ToObject<decimal>().Should().BeGreaterThan(20);

                var nodeBAddress = nodeB.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));

                // Send 10 coins to nodeB.
                var transaction = nodeA.FullNode.WalletTransactionHandler().BuildTransaction(WalletTests.CreateContext(nodeA.FullNode.Network, new WalletAccountReference(walletName, accountName), password, nodeBAddress.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 15));
                nodeA.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
                TestHelper.WaitLoop(() => nodeA.CreateRPCClient().GetRawMempool().Length > 0);

                TestHelper.MineBlocks(nodeA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodeA, nodeB));
                TestHelper.WaitLoop(() => nodeB.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(x => x.Transaction.Amount) > 0);

                RPCClient rpcClientB = nodeB.CreateRPCClient();
                RPCResponse responseB = await rpcClientB.SendCommandAsync(RPCOperations.getbalance, "*");
                responseB.Result.ToObject<decimal>().Should().Be(10);

                responseB = await rpcClientB.SendCommandAsync(RPCOperations.listaddressgroupings);
                var result = responseB.Result.ToObject<AddressGroupingModel[]>();
                result.Count().Should().Be(1);
            }
        }
    }
}
