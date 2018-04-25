using System;
using System.Net;
using NBitcoin;
using NBitcoin.RPC;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
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
            this.Builder = NodeBuilder.Create();
            this.Node = this.Builder.CreateStratisPowNode();
            this.Builder.StartAll();
            this.RpcClient = this.Node.CreateRPCClient();
            this.NetworkPeerClient = this.Node.CreateNetworkPeerClient();
            this.NetworkPeerClient.VersionHandshakeAsync().GetAwaiter().GetResult();

            // Move a wallet file to the right folder and restart the wallet manager to take it into account.
            this.InitializeTestWallet(this.Node.FullNode.DataFolder.WalletPath);
            var walletManager = this.Node.FullNode.NodeService<IWalletManager>() as WalletManager; ;
            walletManager.Start();
        }
    }

    public class RpcTests : IClassFixture<RpcTestFixtureStratis>
    {
        private readonly RpcTestFixtureStratis rpcTestFixture;

        public RpcTests(RpcTestFixtureStratis RpcTestFixture)
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;

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

            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            this.rpcTestFixture.RpcClient.AddNode(endpoint);

            Assert.Single(connectionManager.ConnectionSettings.AddNode);
        }

        [Fact]
        public void CheckRPCFailures()
        {
            var hash = this.rpcTestFixture.RpcClient.GetBestBlockHash();

            try
            {
                this.rpcTestFixture.RpcClient.SendCommand("lol");
                Assert.True(false, "should throw");
            }
            catch (RPCException ex)
            {
                Assert.Equal(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.RPCCode);
            }
            Assert.Equal(hash, Network.RegTest.GetGenesis().GetHash());
            var oldClient = this.rpcTestFixture.RpcClient;
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
                Assert.Equal(RPCErrorCode.RPC_MISC_ERROR, ex.RPCCode);
            }
        }

        /// <summary>
        /// Tests RPC get genesis block hash.
        /// </summary>
        [Fact]
        public void CanGetGenesisBlockHashFromRPC()
        {
            RPCResponse response = this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getblockhash, 0);

            string actualGenesis = (string)response.Result;
            Assert.Equal(Network.RegTest.GetGenesis().GetHash().ToString(), actualGenesis);
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
            Assert.Equal(Network.RegTest.GenesisHash, actualHeader.GetHash(Network.RegTest.NetworkOptions));
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
            var resp = this.rpcTestFixture.RpcClient.SendCommand("getpeerinfo").ResultString;
            Assert.StartsWith("[" + Environment.NewLine + "  {" + Environment.NewLine + "    \"id\": 0," + Environment.NewLine + "    \"addr\": \"[", resp);
        }

        /// <summary>
        /// Tests whether the RPC method "getblockhash" can be called and returns the expected string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetBlockHashByStringArgs()
        {
            var resp = this.rpcTestFixture.RpcClient.SendCommand("getblockhash", "0").ResultString;
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
    }
}