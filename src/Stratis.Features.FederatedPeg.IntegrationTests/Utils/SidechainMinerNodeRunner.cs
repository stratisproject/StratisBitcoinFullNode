using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Collateral;
using Stratis.Features.Collateral.CounterChain;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public class SidechainMinerNodeRunner : NodeRunner
    {
        private readonly IDateTimeProvider timeProvider;

        private readonly Network counterChainNetwork;

        public SidechainMinerNodeRunner(string dataDir, string agent, Network network, Network counterChainNetwork, IDateTimeProvider dateTimeProvider)
            : base(dataDir, agent)
        {
            this.Network = network;

            this.counterChainNetwork = counterChainNetwork;

            this.timeProvider = dateTimeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            IFullNodeBuilder builder = new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .SetCounterChainNetwork(this.counterChainNetwork)
                .UseSmartContractPoAConsensus()
                .UseSmartContractCollateralPoAMining()
                .CheckForPoAMembersCollateral()
                .UseTransactionNotification()
                .UseBlockNotification()
                .UseApi()
                .UseMempool()
                .AddRPC()
                .AddSmartContracts(options =>
                {
                    options.UseReflectionExecutor();
                    options.UsePoAWhitelistedContracts();
                })
                .UseSmartContractWallet()
                .MockIBD()
                .ReplaceTimeProvider(this.timeProvider)
                .AddFastMiningCapability();

            this.FullNode = (FullNode)builder.Build();
        }
    }
}
