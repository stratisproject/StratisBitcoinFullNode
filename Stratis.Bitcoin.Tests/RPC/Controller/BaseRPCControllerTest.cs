using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
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
