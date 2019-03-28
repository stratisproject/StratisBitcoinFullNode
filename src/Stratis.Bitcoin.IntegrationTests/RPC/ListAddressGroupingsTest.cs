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
        const string walletName = "mywallet";
        const string password = "password";
        const string accountName = "account 0";

        CoreNode miner;
        CoreNode receiver;

        [Fact]
        public async Task ListAddressGroupingsAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinRegTest();

                var nodeConfig = new NodeConfigParameters
                {
                    { "-txIndex", "1" }
                };

                this.miner = builder.CreateStratisPowNode(network, agent: "miner", configParameters: nodeConfig).WithWallet().Start();
                this.receiver = builder.CreateStratisPowNode(network, agent: "receiver", configParameters: nodeConfig).WithWallet().Start();

                // Mine blocks to get coins.
                TestHelper.MineBlocks(this.miner, 101);

                // Sync miner with receiver.
                TestHelper.ConnectAndSync(this.miner, this.receiver);

                // Send coins to receiver.
                SendCoins(this.miner, this.receiver, Money.Coins(10));

                // There should now only be one item in receiver's listaddressgroupings response.
                var result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(1);
                result.First().Amount.Should().Be(Money.Coins(10));

                // Send 5 coins to miner from receiver; this will return 5 coins back to a change address on receiver.
                SendCoins(this.receiver, this.miner, Money.Coins(5));

                // Get the change address.
                var receiver_Wallet = this.receiver.FullNode.WalletManager().GetWallet(walletName);
                var firstChangeAddress = receiver_Wallet.GetAllAddresses().First(a => a.IsChangeAddress() && a.Transactions.Any());

                // There should now be 2 items in the listaddressgroupings response and contain the change address.
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(2);
                result.Count(a => a.Address == firstChangeAddress.Address).Should().Be(1);
                result.First().Amount.Should().Be(Money.Coins(0));
                // The change address now contains the balance after sending 5 coins.
                result.First(a => a.Address == firstChangeAddress.Address).Amount.Should().Be(Money.Coins((decimal)4.9999548));

                // Send 5 coins from miner to receiver's change address
                SendCoins(this.miner, this.receiver, Money.Coins(5), firstChangeAddress);

                // There should still only be 2 items in the listaddressgroupings response.
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(2);
                result.First().Amount.Should().Be(Money.Coins(0));
                result.First(a => a.Address == firstChangeAddress.Address).Amount.Should().Be(Money.Coins((decimal)4.9999548 + 5));

                // Send the full balance - 1 from receiver to miner.
                var balance = this.receiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(t => t.Transaction.Amount) - Money.Coins(1);
                SendCoins(this.receiver, this.miner, balance);

                // Get the change address.
                receiver_Wallet = this.receiver.FullNode.WalletManager().GetWallet(walletName);
                var changeAddresses = receiver_Wallet.GetAllAddresses().Where(a => a.IsChangeAddress() && a.Transactions.Any());
                var secondChangeAddress = receiver_Wallet.GetAllAddresses().First(a => a.IsChangeAddress() && a.Transactions.Any() && a.Address != firstChangeAddress.Address);

                // There should now be 3 items in the listaddressgroupings response and contain another change address.
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(3);
                result.Count(a => a.Address == firstChangeAddress.Address).Should().Be(1);
                result.Count(a => a.Address == secondChangeAddress.Address).Should().Be(1);
                result.First(a => a.Address == secondChangeAddress.Address).Amount.Should().Be(Money.Coins((decimal)0.99992520));
            }
        }

        private void SendCoins(CoreNode from, CoreNode to, Money coins, HdAddress toAddress = null)
        {
            // Get a receive address.
            if (toAddress == null)
                toAddress = to.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));

            // Send 10 coins to node.
            var transaction = from.FullNode.WalletTransactionHandler().BuildTransaction(WalletTests.CreateContext(from.FullNode.Network, new WalletAccountReference(walletName, accountName), password, toAddress.ScriptPubKey, coins, FeeType.Medium, 10));
            from.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            TestHelper.WaitLoop(() => from.CreateRPCClient().GetRawMempool().Length > 0);

            // Mine the transaction.
            TestHelper.MineBlocks(this.miner, 10);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(from, to));
            TestHelper.WaitLoop(() => to.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(x => x.Transaction.Amount) > 0);
        }

        private async Task<AddressGroupingModel[]> CallListAddressGroupingsAsync()
        {
            RPCClient client = this.receiver.CreateRPCClient();
            var response = await client.SendCommandAsync(RPCOperations.listaddressgroupings);
            var result = response.Result.ToObject<AddressGroupingModel[]>();
            client = null;

            return result;
        }
    }
}
