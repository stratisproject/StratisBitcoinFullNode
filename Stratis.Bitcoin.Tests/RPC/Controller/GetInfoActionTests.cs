using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.RPC.Controllers;
using Stratis.Bitcoin.RPC.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using static Stratis.Bitcoin.BlockStore.ChainBehavior;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class GetInfoActionTests : FullNodeBaseTest
    {
        [Fact]
        public void CallWithoutDependencies()
        {
            var controller = new FullNodeController();

            GetInfoModel info = controller.GetInfo();

            Assert.NotNull(info);
            Assert.NotNull(info.version);
            Assert.NotNull(info.protocolversion);
            Assert.NotNull(info.blocks);
            Assert.NotNull(info.timeoffset);
            Assert.Null(info.connections);
            Assert.NotNull(info.proxy);
            Assert.NotNull(info.difficulty);
            Assert.NotNull(info.testnet);
            Assert.NotNull(info.relayfee);
            Assert.NotNull(info.errors);
            Assert.Null(info.walletversion);
            Assert.Null(info.balance);
            Assert.Null(info.keypoololdest);
            Assert.Null(info.keypoolsize);
            Assert.Null(info.unlocked_until);
            Assert.Null(info.paytxfee);

        }

        [Fact]
        public void CallWithDependencies()
        {
            string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/GetInfoActionTests/CallWithDependencies");

            var nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = dir;
            this.fullNodeBuilder = new FullNodeBuilder(nodeSettings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
            IFullNode fullNode = this.fullNodeBuilder
                .UseConsensus()
                .UseBlockStore()
                .UseMempool().Build();
            IServiceProvider serviceProvider = fullNode.Services.ServiceProvider;
            var network = serviceProvider.GetService<NBitcoin.Network>();
            var settings = serviceProvider.GetService<NodeSettings>();
            var consensus = serviceProvider.GetService<ConsensusValidator>();
            var chain = serviceProvider.GetService<NBitcoin.ConcurrentChain>();
            var chainState = serviceProvider.GetService<ChainState>();
            var blockStoreManager = serviceProvider.GetService<BlockStoreManager>();
            var mempoolManager = serviceProvider.GetService<MempoolManager>();
            var connectionManager = serviceProvider.GetService<ConnectionManager>();
            var controller = new FullNodeController(fullNode, settings, network, consensus, chain, chainState, blockStoreManager, mempoolManager, connectionManager);

            GetInfoModel info = controller.GetInfo();

            uint expectedProtocolVersion = (uint)NodeSettings.Default().ProtocolVersion;
            double expectedTimeOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalHours;
            var expectedRelayFee = MempoolValidator.MinRelayTxFee.FeePerK.ToUnit(NBitcoin.MoneyUnit.BTC);
            Assert.NotNull(info);
            Assert.Equal(0, info.blocks);
            Assert.NotEqual<uint>(0, info.version);
            Assert.Equal(expectedProtocolVersion, info.protocolversion);
            Assert.Equal(expectedTimeOffset, info.timeoffset);
            Assert.Equal(0, info.connections);
            Assert.NotNull(info.proxy);
            Assert.Equal(0, info.difficulty);
            Assert.False(info.testnet);
            Assert.Equal(expectedRelayFee, info.relayfee);
            Assert.Empty(info.errors);
        }

    }
}
