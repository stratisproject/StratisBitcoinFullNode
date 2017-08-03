using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
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
                var node = builder.CreateStratisNode();
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
                RPCClient rpc = builder.CreateStratisNode().CreateRPCClient();
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
                CoreNode node = builder.CreateStratisNode();
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
                CoreNode nodeA = builder.CreateStratisNode();
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
    }
}
