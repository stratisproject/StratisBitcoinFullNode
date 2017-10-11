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
using System.Threading.Tasks;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class RPCTests
    {
        [Fact]
        public async Task CheckRPCFailuresAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
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
        public async Task CanGetGenesisFromRPCAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                RPCClient rpc = (await builder.CreateStratisPowNodeAsync().ConfigureAwait(false)).CreateRPCClient();
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
        public async Task CanGetBlockHeaderFromRPCAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                CoreNode node = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
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
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a non-empty result.
        /// </summary>
        [Fact]
        public async Task CanGetPeersInfoAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                CoreNode nodeA = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
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
        public async Task CanGetPeersInfoByStringArgsAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                CoreNode nodeA = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    var resp = rpc.SendCommand("getpeerinfo").ResultString;
                    Assert.True(resp.StartsWith("[" + Environment.NewLine + "  {" + Environment.NewLine + "    \"id\": 0," + Environment.NewLine + "    \"addr\": \"["));
                }
            }
        }

        /// <summary>
        /// Tests whether the RPC method "getblockhash" can be called and returns the expected string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public async Task CanGetBlockHashByStringArgsAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                CoreNode nodeA = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
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
        /// Tests whether the RPC method "createindex" can be called and returns the expected string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public async Task CanCreateIndexByStringArgsAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                CoreNode nodeA = await builder.CreateStratisPowNodeAsync(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                    .UseConsensus()
                    .UseIndexStore()
                    .UseMempool()
                    .AddRPC();
                }).ConfigureAwait(false);
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
        /// Tests whether the RPC method "generate" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public async Task CanGenerateByStringArgsAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                CoreNode nodeA = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();
                    var resp = rpc.SendCommand("generate", "1").ResultString;
                    Assert.True(resp.StartsWith("[" + Environment.NewLine + "  \""));
                }
            }
        }

    }
}
