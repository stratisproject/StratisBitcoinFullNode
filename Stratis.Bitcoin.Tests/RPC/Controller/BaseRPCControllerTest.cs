using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.RPC;
using Stratis.Bitcoin.RPC.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public abstract class BaseRPCControllerTest : TestBase
    {
        public IFullNode BuildServicedNode(string dir)
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

            return fullNode;
        }
    }
}
