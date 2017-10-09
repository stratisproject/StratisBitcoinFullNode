using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.IndexStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
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

                // Assert block header fields match
                Assert.Equal(expectedHeader.Version, actualHeader.Version);
                Assert.Equal(expectedHeader.HashPrevBlock, actualHeader.HashPrevBlock);
                Assert.Equal(expectedHeader.HashMerkleRoot, actualHeader.HashMerkleRoot);
                Assert.Equal(expectedHeader.Time, actualHeader.Time);
                Assert.Equal(expectedHeader.Bits, actualHeader.Bits);
                Assert.Equal(expectedHeader.Nonce, actualHeader.Nonce);

                // Assert header hash matches genesis hash
                Assert.Equal(Network.RegTest.GenesisHash, actualHeader.GetHash());
            }
        }

        /// <summary>
        /// Tests RPC getpeersinfo
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
        /// Can call getpeersinfo by string arguments
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
                    Assert.True(resp.StartsWith("[\r\n  {\r\n    \"id\": 0,\r\n    \"addr\": \"["));
                }
            }
        }

        /// <summary>
        /// Can GetBlockHash by string arguments
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
        /// Can CreateIndex by string arguments
        /// </summary>
        [Fact]
        public void CanCreateIndexByStringArgs()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode(false, b =>
                {
                    b
                    .UseConsensus()
                    .UseIndexStore()
                    .UseMempool()
                    .AddRPC();
                });
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    var args = new List<string>();
                    args.Add("testindex");
                    args.Add("false");
                    args.Add("(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })");
                    var resp = rpc.SendCommand("createindex", args.ToArray()).ResultString;

                    Assert.Equal("True", resp);
                }
            }
        }

        /// <summary>
        /// Tests RPC generate
        /// </summary>
        [Fact]
        public void CanGenerateByStringArgs()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode();
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    var resp = rpc.SendCommand("generate", "1").ResultString;
                    Assert.True(resp.StartsWith("[\r\n  \""));
                }
            }
        }

    }
}
