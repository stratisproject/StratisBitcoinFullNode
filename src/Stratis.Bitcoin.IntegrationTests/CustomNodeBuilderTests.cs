using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class CustomNodeBuilderTests
    {
        [Fact]
        public void CanOverridePorts()
        {
            var extraParams = new NodeConfigParameters {{"apiport", "12345"}};
            using (var nodeBuilder = NodeBuilder.Create(this))
            {
                var buildAction = new Action<IFullNodeBuilder>(builder => 
                    builder.UseBlockStore()
                        .UsePowConsensus()
                        .UseMempool()
                        .AddMining()
                        .UseWallet()
                        .AddRPC()
                        .UseApi()
                        .MockIBD());
                var coreNode = nodeBuilder.CreateCustomNode(false, buildAction, Network.StratisRegTest,
                    ProtocolVersion.ALT_PROTOCOL_VERSION, extraParams: extraParams);
                coreNode.Start();
                coreNode.ApiPort.Should().Be(12345);
                coreNode.FullNode.NodeService<ApiSettings>().ApiPort.Should().Be(12345);
                coreNode.RpcPort.Should().NotBe(0);
                coreNode.FullNode.NodeService<RpcSettings>().RPCPort.Should().NotBe(0);


            }
        }
    }
}
