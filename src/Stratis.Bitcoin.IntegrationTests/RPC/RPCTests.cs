using System;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// Stratis test fixture for RPC tests.
    /// </summary>
    public class RpcTestFixtureStratis : RpcTestFixtureBase
    {
        /// <inheritdoc />
        protected override void InitializeFixture()
        {
            this.Builder = NodeBuilder.Create(this);
            this.Node = this.Builder.CreateStratisPowNode(new BitcoinRegTest()).Start();

            this.RpcClient = this.Node.CreateRPCClient();
            this.NetworkPeerClient = this.Node.CreateNetworkPeerClient();
            this.NetworkPeerClient.VersionHandshakeAsync().GetAwaiter().GetResult();

            // Move a wallet file to the right folder and restart the wallet manager to take it into account.
            this.InitializeTestWallet(this.Node.FullNode.DataFolder.WalletPath);
            var walletManager = this.Node.FullNode.NodeService<IWalletManager>() as WalletManager;
            walletManager.Start();
        }
    }

    public class RpcTests : IClassFixture<RpcTestFixtureStratis>
    {
        private readonly RpcTestFixtureStratis rpcTestFixture;

        public RpcTests(RpcTestFixtureStratis RpcTestFixture)
        {
            this.rpcTestFixture = RpcTestFixture;
        }

        /// <summary>
        /// Tests whether the RPC method "addnode" adds a network peer to the connection manager.
        /// </summary>
        [Fact]
        public void CanAddNodeToConnectionManager()
        {
            var connectionManager = this.rpcTestFixture.Node.FullNode.NodeService<IConnectionManager>();
            Assert.Empty(connectionManager.ConnectionSettings.AddNode);

            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            this.rpcTestFixture.RpcClient.AddNode(endpoint);

            Assert.Single(connectionManager.ConnectionSettings.AddNode);
        }

        [Fact]
        public void CheckRPCFailures()
        {
            uint256 hash = this.rpcTestFixture.RpcClient.GetBestBlockHash();

            Assert.Equal(hash, KnownNetworks.RegTest.GetGenesis().GetHash());
            RPCClient oldClient = this.rpcTestFixture.RpcClient;
            var client = new RPCClient("abc:def", this.rpcTestFixture.RpcClient.Address, this.rpcTestFixture.RpcClient.Network);
            try
            {
                client.GetBestBlockHash();
                Assert.True(false, "should throw");
            }
            catch (Exception ex)
            {
                Assert.Contains("401", ex.Message);
            }
            client = oldClient;

            try
            {
                client.SendCommand("addnode", "regreg", "addr");
                Assert.True(false, "should throw");
            }
            catch (RPCException ex)
            {
                Assert.Equal(RPCErrorCode.RPC_INTERNAL_ERROR, ex.RPCCode);
            }
        }

        [Fact]
        public void InvalidCommandSendRPCException()
        {
            var ex = Assert.Throws<RPCException>(() => this.rpcTestFixture.RpcClient.SendCommand("donotexist"));
            Assert.True(ex.RPCCode == RPCErrorCode.RPC_METHOD_NOT_FOUND);
        }

        [Fact]
        public void CanSendCommand()
        {
            RPCResponse response = this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getinfo);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public void CanGetRawMemPool()
        {
            uint256[] ids = this.rpcTestFixture.RpcClient.GetRawMempool();
            Assert.NotNull(ids);
        }

        [Fact]
        public void CanGetBlockCount()
        {
            int blockCount = this.rpcTestFixture.RpcClient.GetBlockCountAsync().Result;
            Assert.Equal(0, blockCount);
        }

        [Fact]
        public void CanGetStratisPeersInfo()
        {
            PeerInfo[] peers = this.rpcTestFixture.RpcClient.GetStratisPeersInfoAsync().Result;
            Assert.NotEmpty(peers);
        }

        /// <summary>
        /// Tests RPC get genesis block hash.
        /// </summary>
        [Fact]
        public void CanGetGenesisBlockHashFromRPC()
        {
            RPCResponse response = this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getblockhash, 0);

            string actualGenesis = (string)response.Result;
            Assert.Equal(KnownNetworks.RegTest.GetGenesis().GetHash().ToString(), actualGenesis);
        }

        /// <summary>
        /// Tests RPC getbestblockhash.
        /// </summary>
        [Fact]
        public void CanGetGetBestBlockHashFromRPC()
        {
            uint256 expected = this.rpcTestFixture.Node.FullNode.Chain.Tip.Header.GetHash();

            uint256 response = this.rpcTestFixture.RpcClient.GetBestBlockHash();

            Assert.Equal(expected, response);
        }

        /// <summary>
        /// Tests RPC getblockheader.
        /// </summary>
        [Fact]
        public void CanGetBlockHeaderFromRPC()
        {
            uint256 hash = this.rpcTestFixture.RpcClient.GetBlockHash(0);
            BlockHeader expectedHeader = this.rpcTestFixture.Node.FullNode.Chain?.GetBlock(hash)?.Header;
            BlockHeader actualHeader = this.rpcTestFixture.RpcClient.GetBlockHeader(0);

            // Assert block header fields match.
            Assert.Equal(expectedHeader.Version, actualHeader.Version);
            Assert.Equal(expectedHeader.HashPrevBlock, actualHeader.HashPrevBlock);
            Assert.Equal(expectedHeader.HashMerkleRoot, actualHeader.HashMerkleRoot);
            Assert.Equal(expectedHeader.Time, actualHeader.Time);
            Assert.Equal(expectedHeader.Bits, actualHeader.Bits);
            Assert.Equal(expectedHeader.Nonce, actualHeader.Nonce);

            // Assert header hash matches genesis hash.
            Assert.Equal(KnownNetworks.RegTest.GenesisHash, actualHeader.GetHash());
        }

        /// <summary>
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a non-empty result.
        /// </summary>
        [Fact]
        public void CanGetPeersInfo()
        {
            PeerInfo[] peers = this.rpcTestFixture.RpcClient.GetPeersInfo();
            Assert.NotEmpty(peers);
        }

        /// <summary>
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetPeersInfoByStringArgs()
        {
            string resp = this.rpcTestFixture.RpcClient.SendCommand("getpeerinfo").ResultString;
            Assert.StartsWith("[" + Environment.NewLine + "  {" + Environment.NewLine + "    \"id\": 0," + Environment.NewLine + "    \"addr\": \"[", resp);
        }

        /// <summary>
        /// Tests whether the RPC method "getblockhash" can be called and returns the expected string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetBlockHashByStringArgs()
        {
            string resp = this.rpcTestFixture.RpcClient.SendCommand("getblockhash", "0").ResultString;
            Assert.Equal("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", resp);
        }

        /// <summary>
        /// Tests whether the RPC method "generate" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGenerateByStringArgs()
        {
            string resp = this.rpcTestFixture.RpcClient.SendCommand("generate", "1").ResultString;
            Assert.StartsWith("[" + Environment.NewLine + "  \"", resp);
        }

        /// <summary>
        /// Tests that RPC method 'SendRawTransaction' can be called with a new transaction.
        /// </summary>
        [Fact]
        public void SendRawTransaction()
        {
            var tx = new Transaction();
            tx.Outputs.Add(new TxOut(Money.Coins(1.0m), new Key()));
            this.rpcTestFixture.RpcClient.SendRawTransaction(tx);
        }

        /// <summary>
        /// Tests that RPC method 'GetNewAddress' can be called and returns an address.
        /// </summary>
        [Fact]
        public void GetNewAddress()
        {
            // Try creating with default parameters.
            BitcoinAddress address = this.rpcTestFixture.RpcClient.GetNewAddress();
            Assert.NotNull(address);

            // Try creating with optional parameters.
            address = BitcoinAddress.Create(this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getnewaddress, new[] { string.Empty, "legacy" }).ResultString, this.rpcTestFixture.RpcClient.Network);
            Assert.NotNull(address);
        }

        [Fact]
        public void TestGetNewAddressWithUnsupportedAddressTypeThrowsRpcException()
        {
            Assert.Throws<RPCException>(() => this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getnewaddress, new[] { string.Empty, "bech32" }));
        }

        [Fact]
        public void TestGetNewAddressWithAccountParameterThrowsRpcException()
        {
            Assert.Throws<RPCException>(() => this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getnewaddress, new[] { "account1", "legacy" }));
        }

        [Fact]
        public async void TestRpcBatchAsync()
        {
            var rpcBatch = this.rpcTestFixture.RpcClient.PrepareBatch();
            var rpc1 = rpcBatch.SendCommandAsync("getpeerinfo");
            var rpc2 = rpcBatch.SendCommandAsync("getrawmempool");
            await rpcBatch.SendBatchAsync();
            var response1 = await rpc1;
            var response1AsString = response1.ResultString;
            Assert.False(string.IsNullOrEmpty(response1AsString));
            var response2 = await rpc2;
            var response2AsString = response2.ResultString;
            Assert.False(string.IsNullOrEmpty(response2AsString));
        }

        // TODO: implement the RPC methods used below
        //[Fact]
        //public void RawTransactionIsConformsToRPC()
        //{
        //   var tx = Transaction.Load("01000000ac55a957010000000000000000000000000000000000000000000000000000000000000000ffffffff0401320103ffffffff010084d717000000001976a9143ac0dad2ad42e35fcd745d7511d47c24ad6580b588ac00000000", network: this.rpcTestFixture.Node.FullNode.Network);
        //   Transaction tx2 = this.rpcTestFixture.RpcClient.GetRawTransaction(uint256.Parse("65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"));
        //   Assert.Equal(tx.GetHash(), tx2.GetHash());
        //}

        //[Fact]
        //public void CanDecodeRawTransaction()
        //{
        //    var tx = this.rpcTestFixture.Node.FullNode.Network.GetGenesis().Transactions[0];
        //    var tx2 = this.rpcTestFixture.RpcClient.DecodeRawTransaction(tx.ToBytes());
        //    Assert.True(JToken.DeepEquals(tx.ToString(RawFormat.Satoshi), tx2.ToString(RawFormat.Satoshi)));
        //}

        //[Fact]
        //public void CanEstimateFeeRate()
        //{
        //    Assert.Throws<NoEstimationException>(() => this.rpcTestFixture.RpcClient.EstimateFeeRate(1));
        //}

        //[Fact]
        //public void TryEstimateFeeRate()
        //{
        //    Assert.Null(this.rpcTestFixture.RpcClient.TryEstimateFeeRate(1));
        //}

        //[Fact]
        //public void CanAddNodes()
        //{

        //    using (var builder = NodeBuilder.Create(this))
        //    {
        //        CoreNode nodeA = builder.CreateStratisPosNode();
        //        CoreNode nodeB = builder.CreateStratisPosNode();
        //        builder.StartAll();

        //        RPCClient rpc = nodeA.CreateRPCClient();
        //        rpc.RemoveNode(nodeA.Endpoint);
        //        rpc.AddNode(nodeB.Endpoint);
        //        Thread.Sleep(500);

        //        AddedNodeInfo[] info = rpc.GetAddedNodeInfo(true);
        //        Assert.NotNull(info);
        //        Assert.NotEmpty(info);

        //        //For some reason this one does not pass anymore in 0.13.1.
        //        //Assert.Equal(nodeB.Endpoint, info.First().Addresses.First().Address);
        //        AddedNodeInfo oneInfo = rpc.GetAddedNodeInfo(true, nodeB.Endpoint);
        //        Assert.NotNull(oneInfo);
        //        Assert.True(oneInfo.AddedNode.ToString() == nodeB.Endpoint.ToString());

        //        oneInfo = rpc.GetAddedNodeInfo(true, nodeA.Endpoint);
        //        Assert.Null(oneInfo);

        //        //rpc.RemoveNode(nodeB.Endpoint);
        //        //Thread.Sleep(500);
        //        //info = rpc.GetAddedNodeInfo(true);
        //        //Assert.Equal(0, info.Count());
        //    }
        //}

        //[Fact]
        //public void CanGetPrivateKeysFromAccount()
        //{
        //    string accountName = "account";
        //    Key key = new Key();
        //    this.rpcTestFixture.RpcClient.ImportAddress(key.PubKey.GetAddress(StratisNetworks.StratisMain), accountName, false);
        //    BitcoinAddress address = this.rpcTestFixture.RpcClient.GetAccountAddress(accountName);
        //    BitcoinSecret secret = this.rpcTestFixture.RpcClient.DumpPrivKey(address);
        //    BitcoinSecret secret2 = this.rpcTestFixture.RpcClient.GetAccountSecret(accountName);

        //    Assert.Equal(secret.ToString(), secret2.ToString());
        //    Assert.Equal(address.ToString(), secret.GetAddress().ToString());
        //}

        //[Fact]
        //public void CanGetPrivateKeysFromAccount()
        //{
        //    string accountName = "account";
        //    Key key = new Key();
        //    this.rpcTestFixture.RpcClient.ImportAddress(key.PubKey.GetAddress(StratisNetworks.StratisMain), accountName, false);
        //    BitcoinAddress address = this.rpcTestFixture.RpcClient.GetAccountAddress(accountName);
        //    BitcoinSecret secret = this.rpcTestFixture.RpcClient.DumpPrivKey(address);
        //    BitcoinSecret secret2 = this.rpcTestFixture.RpcClient.GetAccountSecret(accountName);

        //    Assert.Equal(secret.ToString(), secret2.ToString());
        //    Assert.Equal(address.ToString(), secret.GetAddress().ToString());
        //}

        //[Fact]
        //public void CanBackupWallet()
        //{
        //    var buildOutputDir = Path.GetDirectoryName(".");
        //    var filePath = Path.Combine(buildOutputDir, "wallet_backup.dat");
        //    try
        //    {
        //        this.rpcTestFixture.RpcClient.BackupWallet(filePath);
        //        Assert.True(File.Exists(filePath));
        //    }
        //    finally
        //    {
        //        if (File.Exists(filePath))
        //            File.Delete(filePath);
        //    }
        //}

        //[Fact]
        //public void CanEstimatePriority()
        //{
        //    var priority = this.rpcTestFixture.RpcClient.EstimatePriority(10);
        //    Assert.True(priority > 0 || priority == -1);
        //}
    }
}