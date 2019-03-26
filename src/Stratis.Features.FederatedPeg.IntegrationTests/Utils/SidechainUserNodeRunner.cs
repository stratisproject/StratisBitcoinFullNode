using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public class SidechainUserNodeRunner : NodeRunner
    {

        private readonly IDateTimeProvider timeProvider;

        public SidechainUserNodeRunner(string dataDir, string agent, Network network, IDateTimeProvider dateTimeProvider)
            : base(dataDir, agent)
        {
            this.Network = network;
            this.timeProvider = dateTimeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .AddSmartContracts(options =>
                {
                    options.UseReflectionExecutor();
                })
                .UseSmartContractPoAConsensus()
                .UseSmartContractPoAMining()
                .UseSmartContractWallet()
                .UseMempool()
                .UseApi()
                .MockIBD()
                .AddRPC()
                .ReplaceTimeProvider(this.timeProvider)
                .AddFastMiningCapability()
                .Build();
        }
    }
}
