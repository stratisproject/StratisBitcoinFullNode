using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.IntegrationTests
{
    [TestClass]
    public class RPCTests
    {
        [TestMethod]
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
                    Assert.IsTrue(false, "should throw");
                }
                catch (RPCException ex)
                {
                    Assert.AreEqual(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.RPCCode);
                }
                Assert.AreEqual(hash, Network.RegTest.GetGenesis().GetHash());
                var oldClient = client;
                client = new NBitcoin.RPC.RPCClient("abc:def", client.Address, client.Network);
                try
                {
                    client.GetBestBlockHash();
                    Assert.IsTrue(false, "should throw");
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(ex.Message.Contains("401"));
                }
                client = oldClient;

                try
                {
                    client.SendCommand("addnode", "regreg", "addr");
                    Assert.IsTrue(false, "should throw");
                }
                catch (RPCException ex)
                {
                    Assert.AreEqual(RPCErrorCode.RPC_MISC_ERROR, ex.RPCCode);
                }

            }
        }

    }
}
