using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public class SidechainFederationNodeRunner : NodeRunner
    {
        private bool testingFederation;

        public SidechainFederationNodeRunner(string dataDir, string agent, Network network, bool testingFederation)
            : base(dataDir, agent)
        {
            this.Network = network;

            this.testingFederation = testingFederation;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            var builder = new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .AddSmartContracts(options =>
                {
                    options.UseReflectionExecutor();
                })
                .UseSmartContractWallet()
                .AddFederationGateway()
                .UseFederatedPegPoAMining()
                .UseMempool()
                .UseTransactionNotification()
                .UseBlockNotification()
                .UseApi()
                .AddRPC()
                .MockIBD();

            if (!this.testingFederation)
            {
                builder.UseTestFedPegBlockDefinition();
            }

            this.FullNode = (FullNode) builder.Build();
        }
    }
}
