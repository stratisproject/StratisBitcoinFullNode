using System;
using System.Collections.Generic;
using System.Text;

using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
{
    public class SidechainNodeRunner : NodeRunner
    {
        private IDateTimeProvider timeProvider;

        public SidechainNodeRunner(string dataDir, PoANetwork network, EditableTimeProvider timeProvider)
            : base(dataDir, "")
        {
            this.Network = network;
            this.timeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseFederatedPegPoAMining()
                .UseMempool()
                .UseWallet()
                .UseApi()
                .AddRPC()
                .MockIBD()
                .ReplaceTimeProvider(this.timeProvider)
                .AddFastMiningCapability()
                .Build();
        }
    }
}
