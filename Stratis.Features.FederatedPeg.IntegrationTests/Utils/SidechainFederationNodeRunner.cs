using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
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
                .AddSmartContracts()
                .UseSmartContractWallet()
                .UseReflectionExecutor()
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
