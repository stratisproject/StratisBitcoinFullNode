using System;
using System.IO;
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
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class CustomNodeBuilderTests
    {
        private readonly Network network;

        public CustomNodeBuilderTests()
        {
            this.network = new BitcoinRegTest();
        }

        [Retry(1)]
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, this.network,
                    ProtocolVersion.PROVEN_HEADER_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ApiPort.Should().Be(12345);
                coreNode.FullNode.NodeService<ApiSettings>().ApiPort.Should().Be(12345);

                coreNode.RpcPort.Should().NotBe(0);
                coreNode.FullNode.NodeService<RpcSettings>().RPCPort.Should().NotBe(0);

                coreNode.ProtocolPort.Should().NotBe(0);
                coreNode.FullNode.ConnectionManager.ConnectionSettings.ExternalEndpoint.Port.Should().NotBe(0);
            }
        }

        [Retry(1)]
        public void CanOverrideAllPorts()
        {
            // On MacOS ports below 1024 are privileged, and cannot be bound to by anyone other than root.
            const int port = 1024 + 123;
            const int rpcPort = 1024 + 456;
            const int apiPort = 1024 + 567;

            var extraParams = new NodeConfigParameters
            {
                { "port", port.ToString() },
                { "rpcport", rpcPort.ToString() },
                { "apiport", apiPort.ToString() }
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, this.network,
                    ProtocolVersion.PROVEN_HEADER_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ApiPort.Should().Be(apiPort);
                coreNode.FullNode.NodeService<ApiSettings>().ApiPort.Should().Be(apiPort);

                coreNode.RpcPort.Should().Be(rpcPort);
                coreNode.FullNode.NodeService<RpcSettings>().RPCPort.Should().Be(rpcPort);

                coreNode.ProtocolPort.Should().Be(port);
                coreNode.FullNode.ConnectionManager.ConnectionSettings.ExternalEndpoint.Port.Should().Be(port);
            }
        }

        [Retry(1)]
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, this.network,
                    ProtocolVersion.PROTOCOL_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ConfigParameters["some_new_unknown_param"].Should().Be("with a value");
            }
        }

        [Retry(1)]
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

                var coreNode = nodeBuilder.CreateCustomNode(buildAction, this.network,
                    ProtocolVersion.PROTOCOL_VERSION, configParameters: extraParams);

                coreNode.Start();

                coreNode.ConfigParameters["conf"].Should().Be(specialConf);
                File.Exists(Path.Combine(coreNode.DataFolder, specialConf)).Should().BeTrue();
            }
        }
    }
}
