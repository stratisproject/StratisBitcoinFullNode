using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NBitcoin.Tests
{
    //Require a rpc server on test network running on default port with -rpcuser=NBitcoin -rpcpassword=NBitcoinPassword
    //For me : 
    //"bitcoin-qt.exe" -testnet -server -rpcuser=NBitcoin -rpcpassword=NBitcoinPassword 
    [Trait("RPCClient", "RPCClient")]
    public class RPCClientTests
    {
        const string TestAccount = "NBitcoin.RPCClientTests";
        [Fact]
        public void InvalidCommandSendRPCException()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                AssertException<RPCException>(() => rpc.SendCommand("donotexist"), (ex) =>
                {
                    Assert.True(ex.RPCCode == RPCErrorCode.RPC_METHOD_NOT_FOUND);
                });
            }
        }


        [Fact]
        public void CanSendCommand()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                var response = rpc.SendCommand(RPCOperations.getinfo);
                Assert.NotNull(response.Result);
            }
        }

        [Fact]
        public void CanGetGenesisFromRPC()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                var response = rpc.SendCommand(RPCOperations.getblockhash, 0);
                var actualGenesis = (string)response.Result;
                Assert.Equal(Network.RegTest.GetGenesis().GetHash().ToString(), actualGenesis);
                Assert.Equal(Network.RegTest.GetGenesis().GetHash(), rpc.GetBestBlockHash());
            }
        }

        [Fact]
        public void CanSignRawTransaction()
        {
            using(var builder = NodeBuilder.Create())
            {
                var node = builder.CreateNode();
                var rpc = node.CreateRPCClient();
                builder.StartAll();
                node.CreateRPCClient().Generate(101);

                var tx = new Transaction();
                tx.Outputs.Add(new TxOut(Money.Coins(1.0m), new Key()));
                var funded = node.CreateRPCClient().FundRawTransaction(tx);
                var signed = node.CreateRPCClient().SignRawTransaction(funded.Transaction);
                node.CreateRPCClient().SendRawTransaction(signed);
            }
        }

        [Fact]
        public void CanGetBlockFromRPC()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                var response = rpc.GetBlockHeader(0);
                AssertEx.CollectionEquals(Network.RegTest.GetGenesis().Header.ToBytes(), response.ToBytes());

                response = rpc.GetBlockHeader(0);
                Assert.Equal(Network.RegTest.GenesisHash, response.GetHash());
            }
        }

        [Fact]
        public void TryValidateAddress()
        {
            using (var builder = NodeBuilder.Create())
            {
                CoreNode node = builder.CreateNode();
                node.Start();
                node.Generate(101);
                RPCClient rpc = node.CreateRPCClient();
                // RegTest
                BitcoinAddress pkh = rpc.GetNewAddress();
                Assert.True(rpc.ValidateAddress(pkh).IsValid);
            }
        }

        [Fact]
        public void TryEstimateFeeRate()
        {
            using(var builder = NodeBuilder.Create())
            {
                var node = builder.CreateNode();
                node.Start();
                node.Generate(101);
                var rpc = node.CreateRPCClient();
                Assert.Null(rpc.TryEstimateFeeRate(1));
            }
        }

        [Fact]
        public void CanGetTxOutNoneFromRPC()
        {
            using (var builder = NodeBuilder.Create())
            {
                var node = builder.CreateNode();
                node.Start();
                var rpc = node.CreateRPCClient();
                var txid = rpc.Generate(1).Single();
                var resultTxOut = rpc.GetTxOut(txid, 0, true);
                Assert.Null(resultTxOut);
            }
        }

        [Fact]
        public void CanGetTransactionBlockFromRPC()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                var blockId = rpc.GetBestBlockHash();
                var block = rpc.GetBlock(blockId);
                Assert.True(block.CheckMerkleRoot());
            }
        }

        [Fact]
        public void CanGetPrivateKeysFromAccount()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                Key key = new Key();
                rpc.ImportAddress(key.PubKey.GetAddress(Network.RegTest), TestAccount, false);
                BitcoinAddress address = rpc.GetAccountAddress(TestAccount);
                BitcoinSecret secret = rpc.DumpPrivKey(address);
                BitcoinSecret secret2 = rpc.GetAccountSecret(TestAccount);

                Assert.Equal(secret.ToString(), secret2.ToString());
                Assert.Equal(address.ToString(), secret.GetAddress().ToString());
            }
        }

        [Fact]
        public void CanGetPrivateKeysFromLockedAccount()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode().CreateRPCClient();
                builder.StartAll();
                Key key = new Key();
                var passphrase = "password1234";
                rpc.SendCommand(RPCOperations.encryptwallet, passphrase);
                builder.Nodes[0].Restart();
                rpc.ImportAddress(key.PubKey.GetAddress(Network.RegTest), TestAccount, false);
                BitcoinAddress address = rpc.GetAccountAddress(TestAccount);
                rpc.WalletPassphrase(passphrase, 60);
                BitcoinSecret secret = rpc.DumpPrivKey(address);
                BitcoinSecret secret2 = rpc.GetAccountSecret(TestAccount);

                Assert.Equal(secret.ToString(), secret2.ToString());
                Assert.Equal(address.ToString(), secret.GetAddress().ToString());
            }
        }

        [Fact]
        public void CanDecodeAndEncodeRawTransaction()
        {
            var tests = TestCase.read_json("data/tx_raw.json");
            foreach(var test in tests)
            {
                var format = (RawFormat)Enum.Parse(typeof(RawFormat), (string)test[0], true);
                var network = ((string)test[1]) == "Main" ? Network.Main : Network.TestNet;
                var testData = ((JObject)test[2]).ToString();

                Transaction raw = Transaction.Parse(testData, format, network);

                AssertJsonEquals(raw.ToString(format, network), testData);

                var raw3 = Transaction.Parse(raw.ToString(format, network), format);
                Assert.Equal(raw.ToString(format, network), raw3.ToString(format, network));
            }
        }

        [Fact]
        public void CanDecodeUnspentTransaction()
        {
            var testJson =
@"{
    ""bestblock"": ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""confirmations"": 1,
    ""value"": 7.744E-05,
    ""scriptPubKey"": {
        ""asm"": ""OP_DUP OP_HASH160 fdb12c93cf639eb38d1998959cfd2f35eb730ede OP_EQUALVERIFY OP_CHECKSIG"",
        ""hex"": ""76a914fdb12c93cf639eb38d1998959cfd2f35eb730ede88ac"",
        ""reqSigs"": 1,
        ""type"": ""pubkeyhash"",
        ""addresses"": [
          ""n4eMVrvNqe4EtZDEeei3o63hymTKZNZGhf""
        ]
    },
    ""coinbase"": true
}";
            var testData = JObject.Parse(testJson);
            var unspentTransaction = new UnspentTransaction(testData);
            Assert.Equal(1, unspentTransaction.confirmations);
            Assert.Equal(1, unspentTransaction.scriptPubKey.reqSigs);
            Assert.Single(unspentTransaction.scriptPubKey.addresses);
            Assert.Equal(7.744E-05m, unspentTransaction.value);
        }

        [Fact]
        public void CanDecodeUnspentCoinWatchOnlyAddress()
        {
            var testJson =
@"{
    ""txid"" : ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""vout"" : 1,
    ""address"" : ""mgnucj8nYqdrPFh2JfZSB1NmUThUGnmsqe"",
    ""account"" : ""test label"",
    ""scriptPubKey"" : ""76a9140dfc8bafc8419853b34d5e072ad37d1a5159f58488ac"",
    ""amount"" : 0.00010000,
    ""confirmations"" : 6210,
    ""spendable"" : false
}";
            var testData = JObject.Parse(testJson);
            var unspentCoin = new UnspentCoin(testData, Network.TestNet);

            Assert.Equal("test label", unspentCoin.Account);
            Assert.False(unspentCoin.IsSpendable);
            Assert.Null(unspentCoin.RedeemScript);
        }

        [Fact]
        public void CanDecodeUnspentCoinLegacyPre_0_10_0()
        {
            var testJson =
@"{
    ""txid"" : ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""vout"" : 1,
    ""address"" : ""mgnucj8nYqdrPFh2JfZSB1NmUThUGnmsqe"",
    ""account"" : ""test label"",
    ""scriptPubKey"" : ""76a9140dfc8bafc8419853b34d5e072ad37d1a5159f58488ac"",
    ""amount"" : 0.00010000,
    ""confirmations"" : 6210
}";
            var testData = JObject.Parse(testJson);
            var unspentCoin = new UnspentCoin(testData, Network.TestNet);

            // Versions prior to 0.10.0 were always spendable (but had no JSON field)
            Assert.True(unspentCoin.IsSpendable);
        }

        [Fact]
        public void CanDecodeUnspentCoinWithRedeemScript()
        {
            var testJson =
@"{
    ""txid"" : ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""vout"" : 1,
    ""address"" : ""mgnucj8nYqdrPFh2JfZSB1NmUThUGnmsqe"",
    ""account"" : ""test label"",
    ""scriptPubKey"" : ""76a9140dfc8bafc8419853b34d5e072ad37d1a5159f58488ac"",
    ""redeemScript"" : ""522103310188e911026cf18c3ce274e0ebb5f95b007f230d8cb7d09879d96dbeab1aff210243930746e6ed6552e03359db521b088134652905bd2d1541fa9124303a41e95621029e03a901b85534ff1e92c43c74431f7ce72046060fcf7a95c37e148f78c7725553ae"",
    ""amount"" : 0.00010000,
    ""confirmations"" : 6210,
    ""spendable"" : true
}";
            var testData = JObject.Parse(testJson);
            var unspentCoin = new UnspentCoin(testData, Network.TestNet);

            Console.WriteLine("Redeem Script: {0}", unspentCoin.RedeemScript);
            Assert.NotNull(unspentCoin.RedeemScript);
        }

        [Fact]
        public void RawTransactionIsConformsToRPC()
        {
            using(var builder = NodeBuilder.Create())
            {
                var rpc = builder.CreateNode(true).CreateRPCClient();
                builder.StartAll();
                var tx = Network.TestNet.GetGenesis().Transactions[0];

                var tx2 = rpc.DecodeRawTransaction(tx.ToBytes());
                AssertJsonEquals(tx.ToString(RawFormat.Satoshi), tx2.ToString(RawFormat.Satoshi));
            }
        }
        [Fact]
        public void CanUseBatchedRequests()
        {
            using(var builder = NodeBuilder.Create())
            {
                var nodeA = builder.CreateNode();
                builder.StartAll();
                var rpc = nodeA.CreateRPCClient();
                var blocks = rpc.Generate(10);
                Assert.Throws<InvalidOperationException>(() => rpc.SendBatch());
                rpc = rpc.PrepareBatch();
                List<Task<uint256>> requests = new List<Task<uint256>>();
                for(int i = 1; i < 11; i++)
                {
                    requests.Add(rpc.GetBlockHashAsync(i));
                }
                Thread.Sleep(1000);
                foreach(var req in requests)
                {
                    Assert.Equal(TaskStatus.WaitingForActivation, req.Status);
                }
                rpc.SendBatch();
                rpc = rpc.PrepareBatch();
                int blockIndex = 0;
                foreach(var req in requests)
                {
                    Assert.Equal(blocks[blockIndex], req.Result);
                    Assert.Equal(TaskStatus.RanToCompletion, req.Status);
                    blockIndex++;
                }
                requests.Clear();

                requests.Add(rpc.GetBlockHashAsync(10));
                requests.Add(rpc.GetBlockHashAsync(11));
                requests.Add(rpc.GetBlockHashAsync(9));
                requests.Add(rpc.GetBlockHashAsync(8));
                rpc.SendBatch();
                rpc = rpc.PrepareBatch();
                Assert.Equal(TaskStatus.RanToCompletion, requests[0].Status);
                Assert.Equal(TaskStatus.Faulted, requests[1].Status);
                Assert.Equal(TaskStatus.RanToCompletion, requests[2].Status);
                Assert.Equal(TaskStatus.RanToCompletion, requests[3].Status);
                requests.Clear();

                requests.Add(rpc.GetBlockHashAsync(10));
                requests.Add(rpc.GetBlockHashAsync(11));
                rpc.CancelBatch();
                rpc = rpc.PrepareBatch();
                Thread.Sleep(100);
                Assert.Equal(TaskStatus.Canceled, requests[0].Status);
                Assert.Equal(TaskStatus.Canceled, requests[1].Status);
            }
        }

#if !NOSOCKET
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanParseIpEndpoint()
        {
            var endpoint = Utils.ParseIpEndpoint("google.com:94", 90);
            Assert.Equal(94, endpoint.Port);
            endpoint = Utils.ParseIpEndpoint("google.com", 90);
            Assert.Equal(90, endpoint.Port);
            endpoint = Utils.ParseIpEndpoint("10.10.1.3", 90);
            Assert.Equal("10.10.1.3", endpoint.Address.ToString());
            Assert.Equal(90, endpoint.Port);
            endpoint = Utils.ParseIpEndpoint("10.10.1.3:94", 90);
            Assert.Equal("10.10.1.3", endpoint.Address.ToString());
            Assert.Equal(94, endpoint.Port);
            Assert.Throws<System.Net.Sockets.SocketException>(() => Utils.ParseIpEndpoint("2001:db8:1f70::999:de8:7648:6e8:100", 90));
            endpoint = Utils.ParseIpEndpoint("2001:db8:1f70::999:de8:7648:6e8", 90);
            Assert.Equal("2001:db8:1f70:0:999:de8:7648:6e8", endpoint.Address.ToString());
            Assert.Equal(90, endpoint.Port);
            endpoint = Utils.ParseIpEndpoint("[2001:db8:1f70::999:de8:7648:6e8]:94", 90);
            Assert.Equal("2001:db8:1f70:0:999:de8:7648:6e8", endpoint.Address.ToString());
            Assert.Equal(94, endpoint.Port);
        }

        [Fact]
        public void CanAuthWithCookieFile()
        {
#if NOFILEIO
            Assert.Throws<NotSupportedException>(() => new RPCClient(Network.Main));
#else
            using(var builder = NodeBuilder.Create())
            {
                //Sanity check that it does not throw
#pragma warning disable CS0618
                new RPCClient(new NetworkCredential("toto", "tata:blah"), "localhost:10393", Network.Main);

                var node = builder.CreateNode();
                node.CookieAuth = true;
                node.Start();
                var rpc = node.CreateRPCClient();
                rpc.GetBlockCount();
                node.Restart();
                rpc.GetBlockCount();
                Assert.Throws<ArgumentException>(() => new RPCClient("cookiefile=Data\\tx_valid.json", new Uri("http://localhost/"), Network.RegTest));
                Assert.Throws<FileNotFoundException>(() => new RPCClient("cookiefile=Data\\efpwwie.json", new Uri("http://localhost/"), Network.RegTest));

                rpc = new RPCClient("bla:bla", null as Uri, Network.RegTest);
                Assert.Equal("http://127.0.0.1:" + Network.RegTest.RPCPort + "/", rpc.Address.AbsoluteUri);

                rpc = node.CreateRPCClient();
                rpc = rpc.PrepareBatch();
                var blockCountAsync = rpc.GetBlockCountAsync();
                rpc.SendBatch();
                var blockCount = blockCountAsync.GetAwaiter().GetResult();

                node.Restart();

                rpc = rpc.PrepareBatch();
                blockCountAsync = rpc.GetBlockCountAsync();
                rpc.SendBatch();
                blockCount = blockCountAsync.GetAwaiter().GetResult();

                rpc = new RPCClient("bla:bla", "http://toto/", Network.RegTest);
            }
#endif
        }



        [Fact]
        public void RPCSendRPCException()
        {
            using(var builder = NodeBuilder.Create())
            {
                var node = builder.CreateNode();
                builder.StartAll();
                var rpcClient = node.CreateRPCClient();
                try
                {
                    rpcClient.SendCommand("whatever");
                    Assert.False(true, "Should have thrown");
                }
                catch(RPCException ex)
                {
                    if(ex.RPCCode != RPCErrorCode.RPC_METHOD_NOT_FOUND)
                    {
                        Assert.False(true, "Should have thrown RPC_METHOD_NOT_FOUND");
                    }
                }
            }
        }
#endif
        [Fact]
        public void CanBackupWallet()
        {
            using(var builder = NodeBuilder.Create())
            {
                var node = builder.CreateNode();
                node.Start();
                var buildOutputDir = Path.GetDirectoryName(".");
                var filePath = Path.Combine(buildOutputDir, "wallet_backup.dat");
                try
                {
                    var rpc = node.CreateRPCClient();
                    rpc.BackupWallet(filePath);
                    Assert.True(File.Exists(filePath));
                }
                finally
                {
                    if(File.Exists(filePath))
                        File.Delete(filePath);
                }
            }
        }

        private void AssertJsonEquals(string json1, string json2)
        {
            foreach(var c in new[] { "\r\n", " ", "\t" })
            {
                json1 = json1.Replace(c, "");
                json2 = json2.Replace(c, "");
            }

            Assert.Equal(json1, json2);
        }

        void AssertException<T>(Action act, Action<T> assert) where T : Exception
        {
            try
            {
                act();
                Assert.False(true, "Should have thrown an exception");
            }
            catch(T ex)
            {
                assert(ex);
            }
        }
    }
}
