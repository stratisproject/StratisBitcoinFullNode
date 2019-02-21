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

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class GetRawTransactionTest
    {
        private readonly Network network;

        public GetRawTransactionTest()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public void GetRawTransactionDoesntExistInMempool()
        {
            string txId = "7922666d8f88e3af37cfc88ff410da82f02de913a75891258a808c387ebdee54";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                RPCClient rpc = node.CreateRPCClient();

                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.getrawtransaction, txId); };
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("No such mempool transaction. Use -txindex to enable blockchain transaction queries.");
            }
        }

        [Fact]
        public async Task GetRawTransactionDoesntExistInBlockAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Get the last block we have.
                string lastBlockHash = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                RPCClient rpc = node.CreateRPCClient();

                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.getrawtransaction, uint256.Zero.ToString(), 0, lastBlockHash); };
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("No such transaction found in the provided block.");
            }
        }

        [Fact]
        public void GetRawTransactionWhenBlockNotFound()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Act.
                RPCClient rpc = node.CreateRPCClient();
                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.getrawtransaction, uint256.Zero.ToString(), 0, uint256.Zero.ToString()); };

                // Assert.
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("Block hash not found.");
            }
        }

        [Fact]
        public async Task GetRawTransactionWithGenesisTransactionAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Get the genesis block.
                BlockModel block = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = "0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f", outputJson = true })
                    .GetJsonAsync<BlockModel>();

                RPCClient rpc = node.CreateRPCClient();

                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.getrawtransaction, block.MerkleRoot, 0, block.Hash); };
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("The genesis block coinbase is not considered an ordinary transaction and cannot be retrieved.");
            }
        }


        [Fact]
        public async Task GetRawTransactionWithTransactionIndexedInBlockchainVerboseAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Get the last block we have.
                string lastBlockHash = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockModel tip = await $"http://localhost:{node.ApiPort}/api"
                                .AppendPathSegment("blockstore/block")
                                .SetQueryParams(new { hash = lastBlockHash, outputJson = true })
                                .GetJsonAsync<BlockModel>();

                // Act.
                RPCClient rpc = node.CreateRPCClient();
                RPCResponse response = await rpc.SendCommandAsync(RPCOperations.getrawtransaction, tip.Transactions.First(), 1);

                // Assert.
                TransactionVerboseModel transaction = response.Result.ToObject<TransactionVerboseModel>();
                transaction.TxId.Should().Be(tip.Transactions.First());
                transaction.VOut.First().ScriptPubKey.Addresses.Count.Should().Be(1);
            }
        }

        [Fact]
        public async Task GetRawTransactionWithTransactionIndexedInBlockchainNotVerboseAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Get the last block we have.
                string lastBlockHash = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockTransactionDetailsModel tip = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true, showtransactiondetails = true })
                    .GetJsonAsync<BlockTransactionDetailsModel>();

                // Act.
                RPCClient rpc = node.CreateRPCClient();
                RPCResponse response = await rpc.SendCommandAsync(RPCOperations.getrawtransaction, tip.Transactions.First().TxId);

                // Assert.
                response.ResultString.Should().Be(tip.Transactions.First().Hex);
            }
        }

        [Fact]
        public async Task GetRawTransactionWithTransactionInMempoolAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                //Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();

                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send a transaction.
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

                // Act.
                RPCClient rpc = sendingNode.CreateRPCClient();
                RPCResponse response = await rpc.SendCommandAsync(RPCOperations.getrawtransaction, txId.ToString());

                // Assert.
                response.ResultString.Should().Be(buildTransactionModel.Hex);
            }
        }

        [Fact]
        public async Task GetRawTransactionWithBlockHashVerboseAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Get the last block we have.
                string lastBlockHash = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockModel tip = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true })
                    .GetJsonAsync<BlockModel>();

                // Act.
                RPCClient rpc = node.CreateRPCClient();
                RPCResponse response = await rpc.SendCommandAsync(RPCOperations.getrawtransaction, tip.Transactions.First(), 1, tip.Hash);

                // Assert.
                TransactionVerboseModel transaction = response.Result.ToObject<TransactionVerboseModel>();
                transaction.TxId.Should().Be(tip.Transactions.First());
            }
        }

        [Fact]
        public async Task GetRawTransactionWithBlockHashNonVerboseAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                // Get the last block we have.
                string lastBlockHash = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockTransactionDetailsModel tip = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true, showtransactiondetails = true })
                    .GetJsonAsync<BlockTransactionDetailsModel>();

                // Act.
                RPCClient rpc = node.CreateRPCClient();
                RPCResponse response = await rpc.SendCommandAsync(RPCOperations.getrawtransaction, tip.Transactions.First().TxId, 0, tip.Hash);

                // Assert.
                response.ResultString.Should().Be(tip.Transactions.First().Hex);
            }
        }

        [Fact]
        public async Task GetRawTransactionWithTransactionAndBlockHashInBlockchainAndNotIndexedAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var configParameters = new NodeConfigParameters { { "txindex", "0" } };

                CoreNode node = builder.CreateStratisCustomPowNode(new BitcoinRegTest(), configParameters).WithWallet().Start();
                TestHelper.MineBlocks(node, 5);

                // Get the last block we have.
                string lastBlockHash = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockTransactionDetailsModel tip = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new { hash = lastBlockHash, outputJson = true, showtransactiondetails = true })
                    .GetJsonAsync<BlockTransactionDetailsModel>();

                string txId = tip.Transactions.First().TxId;

                RPCClient rpc = node.CreateRPCClient();
                Func<Task> getTransaction = async () => { await rpc.SendCommandAsync(RPCOperations.getrawtransaction, txId); };
                RPCException exception = getTransaction.Should().Throw<RPCException>().Which;
                exception.RPCCode.Should().Be(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY);
                exception.Message.Should().Be("No such mempool transaction. Use -txindex to enable blockchain transaction queries.");
            }
        }
    }
}