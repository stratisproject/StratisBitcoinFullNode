using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class WalletRPCControllerTest
    {
        private readonly Network network;

        public WalletRPCControllerTest()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public async Task GetTransactionDoesntExistInWalletOrBlockAsync()
        {
            string txId = "f13effbbfc1b3d556dbfa25129e09209c9c57ed2737457f5080b78984a8c8554";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                RPCClient rpc = node.CreateRPCClient();

                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.gettransaction, txId); };
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("Invalid or non-wallet transaction id.");
            }
        }

        [Fact]
        public async Task GetTransactionExistsInBlockButDoesntExistInWalletAsync()
        {
            // Transaction included in block at height 3.
            string txId = "21f3b1fd361f50992db217ea16728acc6b3dbf49c8531e60f8a28d7f3bdf4674";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                //// Check transaction exists in block #3.
                BlockModel block = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = "b1209de1c0347be83bb02a3bf9b70e33b06c82b91e68bc6392e6fb813cd5e4bd", outputJson = true })
                    .GetJsonAsync<BlockModel>();

                block.Transactions.Should().ContainSingle(t => t == txId);

                RPCClient rpc = node.CreateRPCClient();

                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.gettransaction, txId); };
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("Invalid or non-wallet transaction id.");
            }
        }

        [Fact]
        public async Task GetTransactionOnGeneratedTransactionAsync()
        {
            // Transaction included in block at height 3.
            string txId = "21f3b1fd361f50992db217ea16728acc6b3dbf49c8531e60f8a28d7f3bdf4674";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Check transaction exists in block #3.
                BlockModel block = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = "b1209de1c0347be83bb02a3bf9b70e33b06c82b91e68bc6392e6fb813cd5e4bd", outputJson = true })
                    .GetJsonAsync<BlockModel>();

                block.Transactions.Should().ContainSingle(t => t == txId);

                // Act.
                RPCClient rpc = sendingNode.CreateRPCClient();
                RPCResponse walletTx = rpc.SendCommand(RPCOperations.gettransaction, txId);

                // Assert.
                GetTransactionModel result = walletTx.Result.ToObject<GetTransactionModel>();
                result.Amount.Should().Be(new Money(400000000));
                result.Fee.Should().BeNull();
                result.Confirmations.Should().Be(148);
                result.Isgenerated.Should().BeTrue();
                result.TransactionId.Should().Be(uint256.Parse(txId));
                result.BlockHash.ToString().Should().Be("b1209de1c0347be83bb02a3bf9b70e33b06c82b91e68bc6392e6fb813cd5e4bd");
                result.BlockIndex.Should().Be(0);
                result.BlockTime.Should().Be(Utils.UnixTimeToDateTime(1547206662));
                result.TimeReceived.Should().Be(Utils.UnixTimeToDateTime(1547206661));
                result.TransactionTime.Should().Be(Utils.UnixTimeToDateTime(1547206661));
                result.Details.Should().ContainSingle();

                GetTransactionDetailsModel details = result.Details.Single();
                details.Address.Should().Be("TXYdgqNVbHTfW5FsdxqSX6vejBByNEk2Yb");
                details.Amount.Should().Be(new Money(400000000));
                details.Fee.Should().BeNull();
                details.Category.Should().Be(GetTransactionDetailsCategoryModel.Generate);
            }
        }

        [Fact]
        public async Task GetTransactionOnImmatureTransactionAsync()
        {
            // Transaction included in block at height 145.
            string txId = "3a96e0ffd83526b24fc035920fec89f689a778f64cd692b5eda2861cccb1ddba";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Check transaction exists in block #145.
                BlockTransactionDetailsModel block = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = "b8ab1d66febfde4c26d0b4755f7def50a63e06267cefc6d6836651fa8910babc", outputJson = true, showtransactiondetails = true })
                    .GetJsonAsync<BlockTransactionDetailsModel>();

                block.Transactions.Should().ContainSingle(t => t.TxId == txId);

                // Act.
                RPCClient rpc = sendingNode.CreateRPCClient();
                RPCResponse walletTx = rpc.SendCommand(RPCOperations.gettransaction, txId);

                // Assert.
                GetTransactionModel result = walletTx.Result.ToObject<GetTransactionModel>();
                result.Amount.Should().Be(new Money(400000000));
                result.Fee.Should().BeNull();
                result.Confirmations.Should().Be(6);
                result.Isgenerated.Should().BeTrue();
                result.TransactionId.Should().Be(uint256.Parse(txId));
                result.BlockHash.ToString().Should().Be("b8ab1d66febfde4c26d0b4755f7def50a63e06267cefc6d6836651fa8910babc");
                result.BlockIndex.Should().Be(0);
                result.BlockTime.Should().Be(block.Time);
                result.TimeReceived.Should().BeOnOrBefore(block.Time);
                result.Details.Should().ContainSingle();

                GetTransactionDetailsModel details = result.Details.Single();
                details.Address.Should().Be("TXYdgqNVbHTfW5FsdxqSX6vejBByNEk2Yb");
                details.Amount.Should().Be(new Money(400000000));
                details.Fee.Should().BeNull();
                details.Category.Should().Be(GetTransactionDetailsCategoryModel.Immature);
            }
        }

        [Fact]
        public async Task GetTransactionOnUnconfirmedTransactionAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with an Op_Return.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "1"
                        }).ToList(),
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                uint256 txId = buildTransactionModel.TransactionId;

                TestHelper.WaitLoop(() =>
                {
                    WalletHistoryModel history = $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/history")
                    .SetQueryParams(new { walletName = "mywallet", AccountName = "account 0" })
                    .GetAsync()
                    .ReceiveJson<WalletHistoryModel>().GetAwaiter().GetResult();

                    return history.AccountsHistoryModel.First().TransactionsHistory.Any(h => h.Id == txId);
                });

                TestHelper.WaitLoop(() =>
                {
                    WalletHistoryModel history = $"http://localhost:{sendingNode.ApiPort}/api"
                        .AppendPathSegment("wallet/history")
                        .SetQueryParams(new { walletName = "mywallet", AccountName = "account 0" })
                        .GetAsync()
                        .ReceiveJson<WalletHistoryModel>().GetAwaiter().GetResult();

                    return history.AccountsHistoryModel.First().TransactionsHistory.Any(h => h.Id == txId);
                });

                Transaction trx = this.network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);

                RPCClient rpcReceivingNode = receivingNode.CreateRPCClient();
                RPCResponse txReceivingWallet = rpcReceivingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                RPCClient rpcSendingNode = sendingNode.CreateRPCClient();
                RPCResponse txSendingWallet = rpcSendingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                // Assert.
                GetTransactionModel resultSendingWallet = txSendingWallet.Result.ToObject<GetTransactionModel>();
                resultSendingWallet.Amount.Should().Be(new Money(-100000000));
                //resultSendingWallet.Fee.Should().Be(new Money(100000000)); // TODO Uncomment when is available.
                resultSendingWallet.Confirmations.Should().Be(0);
                resultSendingWallet.TransactionId.Should().Be(txId);
                resultSendingWallet.BlockHash.Should().BeNull();
                resultSendingWallet.BlockIndex.Should().BeNull();
                resultSendingWallet.BlockTime.Should().BeNull();
                resultSendingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultSendingWallet.TransactionTime.Should().Be(Utils.UnixTimeToDateTime(trx.Time));
                resultSendingWallet.Details.Count.Should().Be(1);

                GetTransactionDetailsModel detailsSendingWallet = resultSendingWallet.Details.Single();
                detailsSendingWallet.Address.Should().Be(unusedaddresses.Single());
                detailsSendingWallet.Amount.Should().Be(new Money(-100000000));
                //detailsSendingWallet.Fee.Should().Be(new Money(100000000)); // TODO Uncomment when is available.
                detailsSendingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Send);

                GetTransactionModel resultReceivingWallet = txReceivingWallet.Result.ToObject<GetTransactionModel>();
                resultReceivingWallet.Amount.Should().Be(new Money(100000000));
                resultReceivingWallet.Fee.Should().BeNull();
                resultReceivingWallet.Confirmations.Should().Be(0);
                resultReceivingWallet.TransactionId.Should().Be(txId);
                resultReceivingWallet.BlockHash.Should().BeNull();
                resultReceivingWallet.BlockIndex.Should().BeNull();
                resultReceivingWallet.BlockTime.Should().BeNull();
                resultReceivingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultReceivingWallet.TransactionTime.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultReceivingWallet.Details.Should().ContainSingle();

                GetTransactionDetailsModel detailsReceivingWallet = resultReceivingWallet.Details.Single();
                detailsReceivingWallet.Address.Should().Be(unusedaddresses.Single());
                detailsReceivingWallet.Amount.Should().Be(new Money(100000000));
                detailsReceivingWallet.Fee.Should().BeNull();
                detailsReceivingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Receive);
            }
        }

        [Fact]
        public async Task GetTransactionOnConfirmedTransactionAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with an Op_Return.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "1"
                        }).ToList(),
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                uint256 txId = buildTransactionModel.TransactionId;

                // Mine and sync so that we make sure the receiving node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);
                TestHelper.WaitForNodeToSync(sendingNode, receivingNode);

                // Get the block that was mined.
                string lastBlockHash = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockModel tip = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true })
                    .GetJsonAsync<BlockModel>();

                Transaction trx = this.network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);

                RPCClient rpcReceivingNode = receivingNode.CreateRPCClient();
                RPCResponse txReceivingWallet = rpcReceivingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                RPCClient rpcSendingNode = sendingNode.CreateRPCClient();
                RPCResponse txSendingWallet = rpcSendingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                // Assert.
                GetTransactionModel resultSendingWallet = txSendingWallet.Result.ToObject<GetTransactionModel>();
                resultSendingWallet.Amount.Should().Be(new Money(-100000000));
                //resultSendingWallet.Fee.Should().Be(new Money(100000000)); // TODO Uncomment when is available.
                resultSendingWallet.Confirmations.Should().Be(1);
                resultSendingWallet.Isgenerated.Should().BeNull();
                resultSendingWallet.TransactionId.Should().Be(txId);
                resultSendingWallet.BlockHash.Should().Be(uint256.Parse(tip.Hash));
                resultSendingWallet.BlockIndex.Should().Be(1);
                resultSendingWallet.BlockTime.Should().Be(tip.Time);
                resultSendingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultSendingWallet.TransactionTime.Should().Be(Utils.UnixTimeToDateTime(trx.Time));
                resultSendingWallet.Details.Count.Should().Be(1);

                GetTransactionDetailsModel detailsSendingWallet = resultSendingWallet.Details.Single();
                detailsSendingWallet.Address.Should().Be(unusedaddresses.First());
                detailsSendingWallet.Amount.Should().Be(new Money(-100000000));
                //detailsSendingWallet.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.
                detailsSendingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Send);

                GetTransactionModel resultReceivingWallet = txReceivingWallet.Result.ToObject<GetTransactionModel>();
                resultReceivingWallet.Amount.Should().Be(new Money(100000000));
                resultReceivingWallet.Fee.Should().BeNull();
                resultReceivingWallet.Confirmations.Should().Be(1);
                resultReceivingWallet.Isgenerated.Should().BeNull();
                resultReceivingWallet.TransactionId.Should().Be(txId);
                resultReceivingWallet.BlockHash.Should().Be(uint256.Parse(tip.Hash));
                resultReceivingWallet.BlockIndex.Should().Be(1);
                resultReceivingWallet.BlockTime.Should().Be(tip.Time);
                resultReceivingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultReceivingWallet.TransactionTime.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultReceivingWallet.Details.Should().ContainSingle();

                GetTransactionDetailsModel detailsReceivingWallet = resultReceivingWallet.Details.Single();
                detailsReceivingWallet.Address.Should().Be(unusedaddresses.Single());
                detailsReceivingWallet.Amount.Should().Be(new Money(100000000));
                detailsReceivingWallet.Fee.Should().BeNull();
                detailsReceivingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Receive);
            }
        }

        [Fact]
        public async Task GetTransactionOnTransactionReceivedToMultipleAddressesAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 2 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with an Op_Return.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "1"
                        }).ToList()
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                uint256 txId = buildTransactionModel.TransactionId;

                // Mine and sync so that we make sure the receiving node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);
                TestHelper.WaitForNodeToSync(sendingNode, receivingNode);

                // Get the block that was mined.
                string lastBlockHash = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockModel tip = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true })
                    .GetJsonAsync<BlockModel>();

                Transaction trx = this.network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);

                RPCClient rpcReceivingNode = receivingNode.CreateRPCClient();
                RPCResponse txReceivingWallet = rpcReceivingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                RPCClient rpcSendingNode = sendingNode.CreateRPCClient();
                RPCResponse txSendingWallet = rpcSendingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                // Assert.
                GetTransactionModel resultSendingWallet = txSendingWallet.Result.ToObject<GetTransactionModel>();
                resultSendingWallet.Amount.Should().Be(new Money(-200000000));
                //resultSendingWallet.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.
                resultSendingWallet.Confirmations.Should().Be(1);
                resultSendingWallet.Isgenerated.Should().BeNull();
                resultSendingWallet.TransactionId.Should().Be(txId);
                resultSendingWallet.BlockHash.Should().Be(uint256.Parse(tip.Hash));
                resultSendingWallet.BlockIndex.Should().Be(1);
                resultSendingWallet.BlockTime.Should().Be(tip.Time);
                resultSendingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultSendingWallet.TransactionTime.Should().Be(Utils.UnixTimeToDateTime(trx.Time));
                resultSendingWallet.Details.Count.Should().Be(2);

                GetTransactionDetailsModel detailsSendingWalletFirstRecipient = resultSendingWallet.Details.Single(d => d.Address == unusedaddresses.First());
                detailsSendingWalletFirstRecipient.Amount.Should().Be(new Money(-100000000));
                //detailsSendingWalletFirstRecipient.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.

                GetTransactionDetailsModel detailsSendingWalletSecondRecipient = resultSendingWallet.Details.Single(d => d.Address == unusedaddresses.Last());
                detailsSendingWalletSecondRecipient.Amount.Should().Be(new Money(-100000000));
                //detailsSendingWalletSecondRecipient.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.

                // Checking receiver.
                GetTransactionModel resultReceivingWallet = txReceivingWallet.Result.ToObject<GetTransactionModel>();
                resultReceivingWallet.Amount.Should().Be(new Money(200000000));
                resultReceivingWallet.Fee.Should().BeNull();
                resultReceivingWallet.Confirmations.Should().Be(1);
                resultReceivingWallet.Isgenerated.Should().BeNull();
                resultReceivingWallet.TransactionId.Should().Be(txId);
                resultReceivingWallet.BlockHash.Should().Be(uint256.Parse(tip.Hash));
                resultReceivingWallet.BlockIndex.Should().Be(1);
                resultReceivingWallet.BlockTime.Should().Be(tip.Time);
                resultReceivingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultReceivingWallet.TransactionTime.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultReceivingWallet.Details.Count.Should().Be(2);

                GetTransactionDetailsModel firstDetailsReceivingWallet = resultReceivingWallet.Details.Single(d => d.Address == unusedaddresses.First());
                firstDetailsReceivingWallet.Address.Should().Be(unusedaddresses.First());
                firstDetailsReceivingWallet.Amount.Should().Be(new Money(100000000));
                firstDetailsReceivingWallet.Fee.Should().BeNull();
                firstDetailsReceivingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Receive);

                GetTransactionDetailsModel secondDetailsReceivingWallet = resultReceivingWallet.Details.Single(d => d.Address == unusedaddresses.Last());
                secondDetailsReceivingWallet.Address.Should().Be(unusedaddresses.Last());
                secondDetailsReceivingWallet.Amount.Should().Be(new Money(100000000));
                secondDetailsReceivingWallet.Fee.Should().BeNull();
                secondDetailsReceivingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Receive);
            }
        }

        [Fact]
        public async Task GetTransactionOnTransactionSentToOwnAddressAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with an Op_Return.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "1"
                        }).ToList()
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                uint256 txId = buildTransactionModel.TransactionId;

                // Mine so that we make sure the node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);

                // Get the block that was mined.
                string lastBlockHash = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockModel tip = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true })
                    .GetJsonAsync<BlockModel>();

                Transaction trx = this.network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);

                RPCClient rpcSendingNode = sendingNode.CreateRPCClient();
                RPCResponse txSendingWallet = rpcSendingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                // Assert.
                GetTransactionModel resultSendingWallet = txSendingWallet.Result.ToObject<GetTransactionModel>();
                resultSendingWallet.Amount.Should().Be(Money.Zero);
                //resultSendingWallet.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.
                resultSendingWallet.Confirmations.Should().Be(1);
                resultSendingWallet.Isgenerated.Should().BeNull();
                resultSendingWallet.TransactionId.Should().Be(txId);
                resultSendingWallet.BlockHash.Should().Be(uint256.Parse(tip.Hash));
                resultSendingWallet.BlockIndex.Should().Be(1);
                resultSendingWallet.BlockTime.Should().Be(tip.Time);
                resultSendingWallet.TimeReceived.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromMinutes(1));
                resultSendingWallet.TransactionTime.Should().Be(Utils.UnixTimeToDateTime(trx.Time));
                resultSendingWallet.Details.Count.Should().Be(2);

                GetTransactionDetailsModel detailsReceivingWallet = resultSendingWallet.Details.Single(d => d.Category == GetTransactionDetailsCategoryModel.Receive);
                detailsReceivingWallet.Address.Should().Be(unusedaddresses.Single());
                detailsReceivingWallet.Amount.Should().Be(new Money(100000000));
                detailsReceivingWallet.Fee.Should().BeNull();

                GetTransactionDetailsModel secondDetailsReceivingWallet = resultSendingWallet.Details.Single(d => d.Category == GetTransactionDetailsCategoryModel.Send);
                secondDetailsReceivingWallet.Address.Should().Be(unusedaddresses.Single());
                secondDetailsReceivingWallet.Amount.Should().Be(new Money(-100000000));
                //secondDetailsReceivingWallet.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.
            }
        }

        [Fact]
        public async Task GetTransactionOnTransactionSentFromMultipleOutputsAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with an Op_Return.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "98000002"
                        }).ToList(),
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                uint256 txId = buildTransactionModel.TransactionId;

                // Mine so that we make sure the node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);

                // Get the block that was mined.
                string lastBlockHash = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockModel tip = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true })
                    .GetJsonAsync<BlockModel>();

                Transaction trx = this.network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);

                RPCClient rpcSendingNode = sendingNode.CreateRPCClient();
                RPCResponse txSendingWallet = rpcSendingNode.SendCommand(RPCOperations.gettransaction, txId.ToString());

                // Assert.
                GetTransactionModel resultSendingWallet = txSendingWallet.Result.ToObject<GetTransactionModel>();
                resultSendingWallet.Amount.Should().Be(new Money(-9800000200000000));
                //resultSendingWallet.Fee.Should().Be(new Money(-100000000)); // TODO Uncomment when is available.
                resultSendingWallet.Confirmations.Should().Be(1);
                resultSendingWallet.Isgenerated.Should().BeNull();
                resultSendingWallet.TransactionId.Should().Be(txId);
                resultSendingWallet.BlockHash.Should().Be(uint256.Parse(tip.Hash));
                resultSendingWallet.BlockIndex.Should().Be(1);
                resultSendingWallet.BlockTime.Should().Be(tip.Time);
                resultSendingWallet.TimeReceived.Should().BeOnOrBefore(tip.Time);
                resultSendingWallet.TransactionTime.Should().Be(Utils.UnixTimeToDateTime(trx.Time));
                resultSendingWallet.Details.Count.Should().Be(1);

                GetTransactionDetailsModel detailsSendingWallet = resultSendingWallet.Details.Single();
                detailsSendingWallet.Address.Should().Be(unusedaddresses.Single());
                detailsSendingWallet.Amount.Should().Be(new Money(-9800000200000000));
                detailsSendingWallet.Category.Should().Be(GetTransactionDetailsCategoryModel.Send);
            }
        }
    }
}
