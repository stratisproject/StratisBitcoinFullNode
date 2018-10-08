using System;
using System.IO;
using FluentAssertions;
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
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class CustomNodeBuilderTests
    {
        [Retry]
        public void CanOverrideOnlyApiPort()
        {
            var extraParams = new NodeConfigParameters { { "apiport", "12345" } };
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
                var coreNode = nodeBuilder.CreateCustomNode(buildAction, KnownNetworks.StratisRegTest,
                    ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ApiPort.Should().Be(12345);
                coreNode.FullNode.NodeService<ApiSettings>().ApiPort.Should().Be(12345);

                coreNode.RpcPort.Should().NotBe(0);
                coreNode.FullNode.NodeService<RpcSettings>().RPCPort.Should().NotBe(0);

                coreNode.ProtocolPort.Should().NotBe(0);
                coreNode.FullNode.ConnectionManager.ConnectionSettings.ExternalEndpoint.Port.Should().NotBe(0);
            }
        }

        [Fact]
        public void CanOverrideAllPorts()
        {
            var extraParams = new NodeConfigParameters
            {
                { "port", "123" },
                { "rpcport", "456" },
                { "apiport", "567" }
            };
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, KnownNetworks.StratisRegTest,
                    ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ApiPort.Should().Be(567);
                coreNode.FullNode.NodeService<ApiSettings>().ApiPort.Should().Be(567);

                coreNode.RpcPort.Should().Be(456);
                coreNode.FullNode.NodeService<RpcSettings>().RPCPort.Should().Be(456);

                coreNode.ProtocolPort.Should().Be(123);
                coreNode.FullNode.ConnectionManager.ConnectionSettings.ExternalEndpoint.Port.Should().Be(123);
            }
        }

        [Retry]
        public void CanUnderstandUnknownParams()
        {
            var extraParams = new NodeConfigParameters
            {
                { "some_new_unknown_param", "with a value" },
            };
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, KnownNetworks.StratisRegTest,
                    ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ConfigParameters["some_new_unknown_param"].Should().Be("with a value");
            }
        }

        [Fact]
        public void CanUseCustomConfigFileFromParams()
        {
            var specialConf = "special.conf";
            var extraParams = new NodeConfigParameters
            {
                { "conf", specialConf },
            };
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, KnownNetworks.StratisRegTest,
                    ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ConfigParameters["conf"].Should().Be(specialConf);
                File.Exists(Path.Combine(coreNode.DataFolder, specialConf)).Should().BeTrue();
            }
        }
    }
}
