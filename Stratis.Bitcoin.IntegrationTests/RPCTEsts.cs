using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
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

        [Fact]
        public void CanGetBlockFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                RPCClient rpc = builder.CreateStratisNode().CreateRPCClient();
                builder.StartAll();
                BlockHeader response = rpc.GetBlockHeader(0);

                // TODO: This assertion is currently failing
                Assert.Equal(Network.RegTest.GenesisHash, response.GetHash());
            }
        }
    }
}
