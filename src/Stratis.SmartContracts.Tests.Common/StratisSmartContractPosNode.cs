using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoS;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.P2P;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class StratisSmartContractPosNode : NodeRunner
    {
        public StratisSmartContractPosNode(string dataDir, Network network)
            : base(dataDir, null)
        {
            this.Network = network;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            IFullNodeBuilder builder = new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UseBlockStore()
                            .UseMempool()
                            .AddRPC()
                            .AddSmartContracts(options =>
                            {
                                options.UseReflectionExecutor();
                            })
                            .UseSmartContractPosConsensus()
                            .UseSmartContractWallet()
                            .UseSmartContractPosPowMining()
                            .MockIBD()
                            .UseTestChainedHeaderTree()
                            .OverrideDateTimeProviderFor<MiningFeature>();

            if (!this.EnablePeerDiscovery)
            {
                builder.RemoveImplementation<PeerConnectorDiscovery>();
                builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());
            }

            this.FullNode = (FullNode)builder.Build();
        }
    }
}