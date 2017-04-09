﻿using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.RPC;
using Stratis.Bitcoin.RPC.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class RPCControllerTest : TestBase
    {
        [Fact]
        public void CanHaveAllServicesTest()
        {
            string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/RPCControllerTest/CanHaveAllServicesTest");
            IFullNode fullNode = BuildServicedNode(dir);
        }

        public static IFullNode BuildServicedNode(string dir)
        {
            var nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = dir;
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .Build();
            IServiceProvider serviceProvider = fullNode.Services.ServiceProvider;
            var network = serviceProvider.GetService<Network>();
            var settings = serviceProvider.GetService<NodeSettings>();
            var consensusLoop = serviceProvider.GetService<ConsensusLoop>();
            var consensus = serviceProvider.GetService<ConsensusValidator>();
            var chain = serviceProvider.GetService<NBitcoin.ConcurrentChain>();
            var chainState = serviceProvider.GetService<ChainBehavior.ChainState>();
            var blockStoreManager = serviceProvider.GetService<BlockStoreManager>();
            var mempoolManager = serviceProvider.GetService<MempoolManager>();
            var connectionManager = serviceProvider.GetService<ConnectionManager>();
            var controller = new FullNodeController(fullNode, settings, network, consensus, chain, chainState, blockStoreManager, mempoolManager, connectionManager);

            Assert.NotNull(fullNode);
            Assert.NotNull(network);
            Assert.NotNull(settings);
            Assert.NotNull(consensusLoop);
            Assert.NotNull(consensus);
            Assert.NotNull(chain);
            Assert.NotNull(chainState);
            Assert.NotNull(blockStoreManager);
            Assert.NotNull(mempoolManager);
            Assert.NotNull(controller);

            return fullNode;
        }
    }
}
