﻿using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Tests;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// Base class for RPC tests.
    /// </summary>
    public abstract class BaseRPCControllerTest : TestBase
    {
        /// <summary>
        /// Builds a node with basic services and RPC enabled.
        /// </summary>
        /// <param name="dir">Data directory that the node should use.</param>
        /// <returns>Interface to the newly built node.</returns>
        public IFullNode BuildServicedNode(string dir)
        {
            NodeSettings nodeSettings = new NodeSettings().LoadArguments(new string[] { $"-datadir={dir}" });
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .Build();

            return fullNode;
        }
    }
}
