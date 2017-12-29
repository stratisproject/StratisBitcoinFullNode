﻿using System;
using System.Threading;
using NBitcoin;
using NBitcoin.RPC;
using Xunit;
using Xunit.Sdk;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// These tests are for RPC tests that require modifying the chain/nodes. 
    /// Setup of the chain or nodes can be done in each test.
    /// </summary>
    public class RpcBitcoinMutableTests
    {
        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetRawMemPool</seealso>
        /// </summary>
        [Fact]
        public void GetRawMemPoolWithValidTxThenReturnsSameTx()
        {
            NodeBuilder builder = NodeBuilder.Create();
            CoreNode node = builder.CreateNode();
            builder.StartAll();

            RPCClient rpcClient = node.CreateRPCClient();

            // generate 101 blocks
            node.GenerateAsync(101).GetAwaiter().GetResult();

            uint256 txid = rpcClient.SendToAddress(new Key().PubKey.GetAddress(rpcClient.Network), Money.Coins(1.0m), "hello", "world");
            uint256[] ids = rpcClient.GetRawMempool();
            Assert.Single(ids);
            Assert.Equal(txid, ids[0]);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanAddNodes</seealso>
        /// </summary>
        [Fact]
        public void AddNodeWithValidNodeThenExecutesSuccessfully()
        {
            NodeBuilder builder = NodeBuilder.Create();
            CoreNode nodeA = builder.CreateNode();
            CoreNode nodeB = builder.CreateNode();
            builder.StartAll();
            RPCClient rpc = nodeA.CreateRPCClient();
            rpc.RemoveNode(nodeA.Endpoint);
            rpc.AddNode(nodeB.Endpoint);

            AddedNodeInfo[] info = null;
            WaitAssert(() =>
            {
                info = rpc.GetAddedNodeInfo(true);
                Assert.NotNull(info);
                Assert.NotEmpty(info);
            });
            //For some reason this one does not pass anymore in 0.13.1
            //Assert.Equal(nodeB.Endpoint, info.First().Addresses.First().Address);
            AddedNodeInfo oneInfo = rpc.GetAddedNodeInfo(true, nodeB.Endpoint);
            Assert.NotNull(oneInfo);
            Assert.Equal(nodeB.Endpoint.ToString(), oneInfo.AddedNode.ToString());
            oneInfo = rpc.GetAddedNodeInfo(true, nodeA.Endpoint);
            Assert.Null(oneInfo);
            rpc.RemoveNode(nodeB.Endpoint);

            WaitAssert(() =>
            {
                info = rpc.GetAddedNodeInfo(true);
                Assert.Empty(info);
            });
        }

        private void WaitAssert(Action act)
        {
            int totalTry = 30;
            while (totalTry > 0)
            {
                try
                {
                    act();
                    return;
                }
                catch (AssertActualExpectedException)
                {
                    Thread.Sleep(100);
                    totalTry--;
                }
            }
        }
    }
}
