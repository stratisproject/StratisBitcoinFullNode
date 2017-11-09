using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.IO;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class RPCTests
    {
        [Fact]
        public void CheckRPCFailures()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node = builder.CreateStratisPowNode();
                builder.StartAll();
                var client = node.CreateRPCClient();
                var hash = client.GetBestBlockHash();
                try
                {
                    client.SendCommand("lol");
                    Assert.True(false, "should throw");
                }
                catch (RPCException ex)
                {
                    Assert.Equal(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.RPCCode);
                }
                Assert.Equal(hash, Network.RegTest.GetGenesis().GetHash());
                var oldClient = client;
                client = new NBitcoin.RPC.RPCClient("abc:def", client.Address, client.Network);
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
        }

        /// <summary>
        /// Tests RPC getbestblockhash.
        /// </summary>
        [Fact]
        public void CanGetGenesisFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                RPCClient rpc = builder.CreateStratisPowNode().CreateRPCClient();
                builder.StartAll();
                RPCResponse response = rpc.SendCommand(RPCOperations.getblockhash, 0);
                string actualGenesis = (string)response.Result;
                Assert.Equal(Network.RegTest.GetGenesis().GetHash().ToString(), actualGenesis);
                Assert.Equal(Network.RegTest.GetGenesis().GetHash(), rpc.GetBestBlockHash());
            }
        }

        /// <summary>
        /// Tests RPC getblockheader.
        /// </summary>
        [Fact]
        public void CanGetBlockHeaderFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode node = builder.CreateStratisPowNode();
                RPCClient rpc = node.CreateRPCClient();
                builder.StartAll();

                uint256 hash = rpc.GetBlockHash(0);
                BlockHeader expectedHeader = node.FullNode.Chain?.GetBlock(hash)?.Header;
                BlockHeader actualHeader = rpc.GetBlockHeader(0);

                // Assert block header fields match.
                Assert.Equal(expectedHeader.Version, actualHeader.Version);
                Assert.Equal(expectedHeader.HashPrevBlock, actualHeader.HashPrevBlock);
                Assert.Equal(expectedHeader.HashMerkleRoot, actualHeader.HashMerkleRoot);
                Assert.Equal(expectedHeader.Time, actualHeader.Time);
                Assert.Equal(expectedHeader.Bits, actualHeader.Bits);
                Assert.Equal(expectedHeader.Nonce, actualHeader.Nonce);

                // Assert header hash matches genesis hash.
                Assert.Equal(Network.RegTest.GenesisHash, actualHeader.GetHash());
            }
        }

        /// <summary>
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a non-empty result.
        /// </summary>
        [Fact]
        public void CanGetPeersInfo()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode();
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    PeerInfo[] peers = rpc.GetPeersInfo();
                    Assert.NotEmpty(peers);
                }
            }
        }

        /// <summary>
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetPeersInfoByStringArgs()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode();
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    var resp = rpc.SendCommand("getpeerinfo").ResultString;
                    Assert.StartsWith("[" + Environment.NewLine + "  {" + Environment.NewLine + "    \"id\": 0," + Environment.NewLine + "    \"addr\": \"[", resp);
                }
            }
        }

        /// <summary>
        /// Tests whether the RPC method "getblockhash" can be called and returns the expected string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetBlockHashByStringArgs()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode();
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    var resp = rpc.SendCommand("getblockhash", "0").ResultString;
                    Assert.Equal("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", resp);
                }
            }
        }

        /// <summary>
        /// Tests whether the RPC method "generate" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGenerateByStringArgs()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode();
                this.InitializeTestWallet(nodeA);
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    string resp = rpc.SendCommand("generate", "1").ResultString;
                    Assert.StartsWith("[" + Environment.NewLine + "  \"", resp);
                }
            }
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isnt' already present.
        /// </summary>
        /// <param name="node">Core node for the test.</param>
        private void InitializeTestWallet(CoreNode node)
        {
            string testWalletPath = Path.Combine(node.DataFolder, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}
