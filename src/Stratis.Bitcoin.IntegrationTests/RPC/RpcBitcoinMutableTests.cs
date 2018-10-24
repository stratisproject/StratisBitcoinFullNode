using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// These tests are for RPC tests that require modifying the chain/nodes. 
    /// Setup of the chain or nodes can be done in each test.
    /// </summary>
    public class RpcBitcoinMutableTests
    {
        private const string BitcoinCoreVersion15 = "0.15.1";
        private readonly Network regTest;
        private readonly Network testNet;

        public RpcBitcoinMutableTests()
        {
            this.regTest = KnownNetworks.RegTest;
            this.testNet = KnownNetworks.TestNet;
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetRawMemPool</seealso>
        /// </summary>
        [Fact]
        public void GetRawMemPoolWithValidTxThenReturnsSameTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode().Start();

                RPCClient rpcClient = node.CreateRPCClient();

                // generate 101 blocks
                node.GenerateAsync(101).GetAwaiter().GetResult();

                uint256 txid = rpcClient.SendToAddress(new Key().PubKey.GetAddress(rpcClient.Network), Money.Coins(1.0m), "hello", "world");
                uint256[] ids = rpcClient.GetRawMempool();
                Assert.Single(ids);
                Assert.Equal(txid, ids[0]);
            }
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanAddNodes</seealso>
        /// </summary>
        [Fact]
        public void CanAddRemoveNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode nodeA = builder.CreateBitcoinCoreNode().Start();
                CoreNode nodeB = builder.CreateBitcoinCoreNode().Start();

                RPCClient rpc = nodeA.CreateRPCClient();
                rpc.RemoveNodeAsync(nodeA.Endpoint);
                rpc.AddNode(nodeB.Endpoint);

                AddedNodeInfo[] info = null;
                TestHelper.WaitLoop(() =>
                {
                    info = rpc.GetAddedNodeInfo(true);
                    return info != null && info.Length > 0;
                });
                Assert.NotNull(info);
                Assert.NotEmpty(info);

                //For some reason this one does not pass anymore in 0.13.1
                //Assert.Equal(nodeB.Endpoint, info.First().Addresses.First().Address);
                AddedNodeInfo oneInfo = rpc.GetAddedNodeInfo(true, nodeB.Endpoint);
                Assert.NotNull(oneInfo);
                Assert.Equal(nodeB.Endpoint.ToString(), oneInfo.AddedNode.ToString());
                oneInfo = rpc.GetAddedNodeInfo(true, nodeA.Endpoint);
                Assert.Null(oneInfo);
                rpc.RemoveNode(nodeB.Endpoint);

                TestHelper.WaitLoop(() =>
                {
                    info = rpc.GetAddedNodeInfo(true);
                    return info.Length == 0;
                });

                Assert.Empty(info);
            }
        }

        [Fact]
        public void CanSendCommand()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                RPCResponse response = rpcClient.SendCommand(RPCOperations.getinfo);
                Assert.NotNull(response.Result);
            }
        }

        [Fact]
        public void CanGetGenesisFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                RPCResponse response = rpcClient.SendCommand(RPCOperations.getblockhash, 0);
                string actualGenesis = (string)response.Result;
                Assert.Equal(this.regTest.GetGenesis().GetHash().ToString(), actualGenesis);
                Assert.Equal(this.regTest.GetGenesis().GetHash(), rpcClient.GetBestBlockHash());
            }
        }

        [Fact]
        public void CanSignRawTransaction()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();
                rpcClient.Generate(101);

                var tx = new Transaction();
                tx.Outputs.Add(new TxOut(Money.Coins(1.0m), new Key()));
                FundRawTransactionResponse funded = node.CreateRPCClient().FundRawTransaction(tx);
                Transaction signed = node.CreateRPCClient().SignRawTransaction(funded.Transaction);
                node.CreateRPCClient().SendRawTransaction(signed);
            }
        }

        [Fact]
        public void CanGetBlockFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                BlockHeader response = rpcClient.GetBlockHeader(0);
                Assert.Equal(this.regTest.GetGenesis().Header.ToBytes(), response.ToBytes());

                response = rpcClient.GetBlockHeader(0);
                Assert.Equal(this.regTest.GenesisHash, response.GetHash());
            }
        }

        [Fact]
        public void TryValidateAddress()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                // RegTest
                BitcoinAddress pkh = rpcClient.GetNewAddress();
                Assert.True(rpcClient.ValidateAddress(pkh).IsValid);
            }
        }

        [Fact]
        public void TryEstimateFeeRate()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                Assert.Null(rpcClient.TryEstimateFeeRate(1));
            }
        }

        [Fact]
        public void CanGetTxOutNoneFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                uint256 txid = rpcClient.Generate(1).Single();
                UnspentTransaction resultTxOut = rpcClient.GetTxOut(txid, 0, true);
                Assert.Null(resultTxOut);
            }
        }

        [Fact]
        public void CanGetTransactionBlockFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                uint256 blockId = rpcClient.GetBestBlockHash();
                Block block = rpcClient.GetBlock(blockId);
                Assert.True(block.CheckMerkleRoot());
            }
        }

        [Fact]
        public void RawTransactionIsConformsToRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                Transaction tx = this.testNet.GetGenesis().Transactions[0];
                Transaction tx2 = rpcClient.DecodeRawTransaction(tx.ToBytes());

                Assert.True(JToken.DeepEquals(tx.ToString(this.testNet, RawFormat.Satoshi), tx2.ToString(this.testNet, RawFormat.Satoshi)));
            }
        }
        [Fact]
        public void CanUseBatchedRequests()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                uint256[] blocks = rpcClient.Generate(10);
                Assert.Throws<InvalidOperationException>(() => rpcClient.SendBatch());
                rpcClient = rpcClient.PrepareBatch();
                var requests = new List<Task<uint256>>();
                for (int i = 1; i < 11; i++)
                {
                    requests.Add(rpcClient.GetBlockHashAsync(i));
                }
                Thread.Sleep(1000);
                foreach (Task<uint256> req in requests)
                {
                    Assert.Equal(TaskStatus.WaitingForActivation, req.Status);
                }
                rpcClient.SendBatch();
                rpcClient = rpcClient.PrepareBatch();
                int blockIndex = 0;
                foreach (Task<uint256> req in requests)
                {
                    Assert.Equal(blocks[blockIndex], req.Result);
                    Assert.Equal(TaskStatus.RanToCompletion, req.Status);
                    blockIndex++;
                }
                requests.Clear();

                requests.Add(rpcClient.GetBlockHashAsync(10));
                requests.Add(rpcClient.GetBlockHashAsync(11));
                requests.Add(rpcClient.GetBlockHashAsync(9));
                requests.Add(rpcClient.GetBlockHashAsync(8));
                rpcClient.SendBatch();
                rpcClient = rpcClient.PrepareBatch();
                Assert.Equal(TaskStatus.RanToCompletion, requests[0].Status);
                Assert.Equal(TaskStatus.Faulted, requests[1].Status);
                Assert.Equal(TaskStatus.RanToCompletion, requests[2].Status);
                Assert.Equal(TaskStatus.RanToCompletion, requests[3].Status);
                requests.Clear();

                requests.Add(rpcClient.GetBlockHashAsync(10));
                requests.Add(rpcClient.GetBlockHashAsync(11));
                rpcClient.CancelBatch();
                rpcClient = rpcClient.PrepareBatch();
                Thread.Sleep(100);
                Assert.Equal(TaskStatus.Canceled, requests[0].Status);
                Assert.Equal(TaskStatus.Canceled, requests[1].Status);
            }
        }

        [Fact]
        public void CanBackupWallet()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                string buildOutputDir = Path.GetDirectoryName(".");
                string filePath = Path.Combine(buildOutputDir, "wallet_backup.dat");
                try
                {
                    rpcClient.BackupWallet(filePath);
                    Assert.True(File.Exists(filePath));
                }
                finally
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
            }
        }

        [Fact]
        public void CanGetPrivateKeysFromAccount()
        {
            string accountName = "account";
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                var key = new Key();
                rpcClient.ImportAddress(key.PubKey.GetAddress(this.regTest), accountName, false);
                BitcoinAddress address = rpcClient.GetAccountAddress(accountName);
                BitcoinSecret secret = rpcClient.DumpPrivKey(address);
                BitcoinSecret secret2 = rpcClient.GetAccountSecret(accountName);

                Assert.Equal(secret.ToString(), secret2.ToString());
                Assert.Equal(address.ToString(), secret.GetAddress().ToString());
            }
        }

        [Fact]
        public async Task CanGetPrivateKeysFromLockedAccountAsync()
        {
            string accountName = "account";
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15).Start();

                RPCClient rpcClient = node.CreateRPCClient();

                var key = new Key();
                string passphrase = "password1234";
                rpcClient.SendCommand(RPCOperations.encryptwallet, passphrase);

                // Wait for recepient to process the command.
                await Task.Delay(300);

                builder.Nodes[0].Restart();
                rpcClient = node.CreateRPCClient();
                rpcClient.ImportAddress(key.PubKey.GetAddress(this.regTest), accountName, false);
                BitcoinAddress address = rpcClient.GetAccountAddress(accountName);
                rpcClient.WalletPassphrase(passphrase, 60);
                BitcoinSecret secret = rpcClient.DumpPrivKey(address);
                BitcoinSecret secret2 = rpcClient.GetAccountSecret(accountName);

                Assert.Equal(secret.ToString(), secret2.ToString());
                Assert.Equal(address.ToString(), secret.GetAddress().ToString());
            }
        }

        [Fact]
        public void CanAuthWithCookieFile()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateBitcoinCoreNode(version: BitcoinCoreVersion15, useCookieAuth: true).Start();

                RPCClient rpcClient = node.CreateRPCClient();
                rpcClient.GetBlockCount();
                node.Restart();
                rpcClient = node.CreateRPCClient();
                rpcClient.GetBlockCount();

                string invalidCookiePath = Path.Combine("Data","invalid.cookie");
                string notFoundCookiePath = Path.Combine("Data", "not_found.cookie");
                Assert.Throws<ArgumentException>(() => new RPCClient($"cookiefile={invalidCookiePath}", new Uri("http://localhost/"), this.regTest));
                Assert.Throws<FileNotFoundException>(() => new RPCClient($"cookiefile={notFoundCookiePath}", new Uri("http://localhost/"), this.regTest));

                rpcClient = new RPCClient("bla:bla", null as Uri, this.regTest);
                Assert.Equal("http://127.0.0.1:" + this.regTest.RPCPort + "/", rpcClient.Address.AbsoluteUri);

                rpcClient = node.CreateRPCClient();
                rpcClient = rpcClient.PrepareBatch();
                Task<int> blockCountAsync = rpcClient.GetBlockCountAsync();
                rpcClient.SendBatch();
                int blockCount = blockCountAsync.GetAwaiter().GetResult();

                node.Restart();

                rpcClient = rpcClient.PrepareBatch();
                blockCountAsync = rpcClient.GetBlockCountAsync();
                rpcClient.SendBatch();
                blockCount = blockCountAsync.GetAwaiter().GetResult();

                rpcClient = new RPCClient("bla:bla", "http://toto/", this.regTest);
            }
        }
    }
}
