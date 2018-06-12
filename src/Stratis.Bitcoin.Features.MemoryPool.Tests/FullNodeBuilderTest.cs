using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    public class FullNodeBuilderTest
    {
        [Fact]
        public void CanHaveAllFullnodeServicesTest()
        {
            // This test is put in the mempool feature because the 
            // mempool requires all the features to be a fullnode


            var nodeSettings = new NodeSettings(args: new string[] {
                $"-datadir=Stratis.Bitcoin.Features.MemoryPool.Tests/TestData/FullNodeBuilderTest/CanHaveAllServicesTest" });
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseBlockStore()
                .UsePowConsensus()
                .UseMempool()
                .Build();

            IServiceProvider serviceProvider = fullNode.Services.ServiceProvider;
            var network = serviceProvider.GetService<Network>();
            var settings = serviceProvider.GetService<NodeSettings>();
            var consensusLoop = serviceProvider.GetService<IConsensusLoop>() as ConsensusLoop;
            var chain = serviceProvider.GetService<NBitcoin.ConcurrentChain>();
            var chainState = serviceProvider.GetService<IChainState>() as ChainState;
            var blockStoreManager = serviceProvider.GetService<BlockStoreManager>();
            var consensusRules = serviceProvider.GetService<IConsensusRules>();
            consensusRules.Register(serviceProvider.GetService<IRuleRegistration>());
            var mempoolManager = serviceProvider.GetService<MempoolManager>();
            var connectionManager = serviceProvider.GetService<IConnectionManager>() as ConnectionManager;

            Assert.NotNull(fullNode);
            Assert.NotNull(network);
            Assert.NotNull(settings);
            Assert.NotNull(consensusLoop);
            Assert.NotNull(chain);
            Assert.NotNull(chainState);
            Assert.NotNull(blockStoreManager);
            Assert.NotNull(mempoolManager);
            Assert.NotNull(connectionManager);
        }
    }
}
