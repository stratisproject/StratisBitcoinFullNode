using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisBitcoinPowRunner : NodeRunner
    {
        public StratisBitcoinPowRunner(string dataDir, Network network, string agent)
            : base(dataDir, agent)
        {
            this.Network = network;
        }

        public override void BuildNode()
        {
            NodeSettings settings = null;

            if (string.IsNullOrEmpty(this.Agent))
                settings = new NodeSettings(this.Network, args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder });
            else
                settings = new NodeSettings(this.Network, agent: this.Agent, args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder });

            var builder = new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UseBlockStore()
                            .UsePowConsensus()
                            .UseMempool()
                            .AddMining()
                            .UseWallet()
                            .AddRPC()
                            .UseApi()
                            .UseTestChainedHeaderTree()
                            .MockIBD();

            ConfigureInterceptors(builder);

            if (this.ServiceToOverride != null)
                builder.OverrideService<BaseFeature>(this.ServiceToOverride);

            if (!this.EnablePeerDiscovery)
            {
                builder.RemoveImplementation<PeerConnectorDiscovery>();
                builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());
            }

            if (this.AlwaysFlushBlocks)
            {
                builder.ReplaceService<IBlockStoreQueueFlushCondition, BlockStoreFeature>(new BlockStoreAlwaysFlushCondition());
            }

            this.FullNode = (FullNode)builder.Build();
        }
    }
}