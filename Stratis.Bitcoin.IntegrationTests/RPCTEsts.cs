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
                    Assert.True(ex.Message.Contains("401"));
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

    }
}
