﻿using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Tests;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class RPCSettingsTest : TestBase
    {
        [Fact]
        public void CanSpecifyRPCSettings()
        {
            var initialBlockSignature = Block.BlockSignature;

            try
            {
                Block.BlockSignature = false;
                var dir = CreateTestDir(this);

                NodeSettings nodeSettings = new NodeSettings().LoadArguments(new string[] { $"-datadir={dir}" });

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseConsensus()
                    .AddRPC(x =>
                    {
                        x.RpcUser = "abc";
                        x.RpcPassword = "def";
                        x.RPCPort = 91;
                    })
                    .Build();

                var settings = node.NodeService<RpcSettings>();

                settings.Load(nodeSettings);

                Assert.Equal("abc", settings.RpcUser);
                Assert.Equal("def", settings.RpcPassword);
                Assert.Equal(91, settings.RPCPort);
            }
            finally
            {
                Block.BlockSignature = initialBlockSignature;
            }

        }
    }
}
