using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
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

                this.miner = builder.CreateStratisPowNode(network, agent: "la-1-miner", configParameters: nodeConfig).WithWallet().Start();
                this.receiver = builder.CreateStratisPowNode(network, agent: "la-1-receiver", configParameters: nodeConfig).WithWallet().Start();

                // Mine blocks to get coins.
                TestHelper.MineBlocks(this.miner, 101);

                // Sync miner with receiver.
                TestHelper.ConnectAndSync(this.miner, this.receiver);

                // Send 10 coins from miner to receiver.
                SendCoins(this.miner, this.receiver, Money.Coins(10));

                // Receiver's listaddressgroupings response contains 1 array with 1 item.
                var result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(1);
                result[0].AddressGroups.First().Amount.Should().Be(Money.Coins(10));
                var receiverAddress = result[0].AddressGroups.First().Address;

                // Send 5 coins to miner from receiver; this will return 5 coins back to a change address on receiver.
                SendCoins(this.receiver, this.miner, Money.Coins(5));

                // Get the change address.
                var receiver_Wallet = this.receiver.FullNode.WalletManager().GetWallet(walletName);
                var firstChangeAddress = receiver_Wallet.GetAllAddresses().First(a => a.IsChangeAddress() && a.Transactions.Any());

                //---------------------------------------------------
                //  Receiver's listaddressgroupings response contains 1 array with 2 items:
                //  - The initial receive address
                //  - The change address address
                //---------------------------------------------------
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(1);
                result[0].AddressGroups.Count().Should().Be(2);
                result[0].AddressGroups.First(a => a.Address == receiverAddress).Amount.Should().Be(Money.Coins(0)); // Initial receive address balance should be 0.
                result[0].AddressGroups.First(a => a.Address == firstChangeAddress.Address).Amount.Should().Be(Money.Coins((decimal)4.9999548)); // Change address balance after sending 5 coins.
                //---------------------------------------------------

                // Send 5 coins from miner to receiver's change address
                SendCoins(this.miner, this.receiver, Money.Coins(5), firstChangeAddress);

                //---------------------------------------------------
                //  Receiver's listaddressgroupings response contains 1 array with 2 items:
                //  - The initial receive address
                //  - The change address address
                //---------------------------------------------------
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(1);
                result[0].AddressGroups.Count().Should().Be(2);
                result[0].AddressGroups.First(a => a.Address == receiverAddress).Amount.Should().Be(Money.Coins(0)); // Initial receive address balance should be 0.
                result[0].AddressGroups.First(a => a.Address == firstChangeAddress.Address).Amount.Should().Be(Money.Coins((decimal)4.9999548 + 5)); // Change address balance + 5 coins.
                //---------------------------------------------------

                // Send the (full balance - 1) from receiver to miner.
                var balance = this.receiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(t => t.Transaction.Amount) - Money.Coins(1);
                SendCoins(this.receiver, this.miner, balance);

                // Get the change address.
                receiver_Wallet = this.receiver.FullNode.WalletManager().GetWallet(walletName);
                var changeAddresses = receiver_Wallet.GetAllAddresses().Where(a => a.IsChangeAddress() && a.Transactions.Any());
                var secondChangeAddress = receiver_Wallet.GetAllAddresses().First(a => a.IsChangeAddress() && a.Transactions.Any() && a.Address != firstChangeAddress.Address);

                //---------------------------------------------------
                //  Receiver's listaddressgroupings response contains 1 array with 3 items:
                //  - The initial receive address
                //  - The change address address
                //  - The change address of sending the full balance - 1
                //---------------------------------------------------
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(1);
                result[0].AddressGroups.Count().Should().Be(3);
                result[0].AddressGroups.Count(a => a.Address == firstChangeAddress.Address).Should().Be(1);
                result[0].AddressGroups.Count(a => a.Address == secondChangeAddress.Address).Should().Be(1);
                result[0].AddressGroups.First(a => a.Address == secondChangeAddress.Address).Amount.Should().Be(Money.Coins((decimal)0.99992520));
                //---------------------------------------------------

                // Send 5 coins to a new unused address on the receiver's wallet.
                SendCoins(this.miner, this.receiver, Money.Coins(5));

                // Receiver's listaddressgroupings response contains 2 arrays:
                //  - Array 1 > The initial receive address
                //  - Array 1 > The change address address
                //  - Array 1 > The change address of sending the full balance - 1
                //  - Array 2 > The receive address of the new transaction
                result = await CallListAddressGroupingsAsync();
                result.Count().Should().Be(2);
                result.Where(r => r.AddressGroups.Count() == 1).First().AddressGroups[0].Amount.Should().Be(Money.Coins(5));
            }
        }

        private void SendCoins(CoreNode from, CoreNode to, Money coins, HdAddress toAddress = null)
        {
            // Get a receive address.
            if (toAddress == null)
                toAddress = to.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));

            // Send 10 coins to node.
            var transaction = from.FullNode.WalletTransactionHandler().BuildTransaction(WalletTests.CreateContext(from.FullNode.Network, new WalletAccountReference(walletName, accountName), password, toAddress.ScriptPubKey, coins, FeeType.Medium, 10));
            from.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            TestBase.WaitLoop(() => from.CreateRPCClient().GetRawMempool().Length > 0);

            // Mine the transaction.
            TestHelper.MineBlocks(this.miner, 10);
            TestBase.WaitLoop(() => TestHelper.AreNodesSynced(from, to));
            TestBase.WaitLoop(() => to.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(x => x.Transaction.Amount) > 0);
        }

        private async Task<List<AddressGroupingModel>> CallListAddressGroupingsAsync()
        {
            RPCClient client = this.receiver.CreateRPCClient();
            var response = await client.SendCommandAsync(RPCOperations.listaddressgroupings);
            var result = response.Result.ToObject<List<object>>();
            client = null;

            // Convert object to model.
            var addressGroupingModels = new List<AddressGroupingModel>();

            foreach (var item in result)
            {
                var addressGroupingModel = new AddressGroupingModel();
                foreach (var inner in JToken.FromObject(item))
                {
                    var innerToken = JToken.FromObject(inner);
                    var address = innerToken.Children().ElementAt(0).Value<string>();
                    var amount = innerToken.Children().ElementAt(1).Value<long>();
                    var addressGroupModel = new AddressGroupModel() { Address = address, Amount = Money.Satoshis(amount) };
                    addressGroupingModel.AddressGroups.Add(addressGroupModel);
                }

                addressGroupingModels.Add(addressGroupingModel);
            }

            return addressGroupingModels;
        }
    }
}
