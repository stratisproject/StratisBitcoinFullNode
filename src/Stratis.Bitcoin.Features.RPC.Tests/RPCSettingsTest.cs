﻿using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Tests;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCSettingsTest : TestBase
    {
        public RPCSettingsTest()
            :base()
        {
            // These are expected to be false for non-POS test cases.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

        [Fact]
        public void CanSpecifyRPCSettings()
        {
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
    }
}
