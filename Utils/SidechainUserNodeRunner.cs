using System;
using System.Collections.Generic;
using System.Text;
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
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.FederatedPeg.Features.FederationGateway;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
{
    /// <summary>
    /// Currently identical to <see cref="SidechainFederationNodeRunner"/> but will be lighter after fixing
    /// https://github.com/stratisproject/FederatedSidechains/issues/273
    /// </summary>
    public class SidechainUserNodeRunner : NodeRunner
    {
        public SidechainUserNodeRunner(string dataDir, string agent, Network network)
            : base(dataDir, agent)
        {
            this.Network = network;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .AddSmartContracts()
                .UseSmartContractPoAConsensus()
                .UseSmartContractPoAMining()
                .UseSmartContractWallet()
                .UseReflectionExecutor()
                .UseMempool()
                .UseApi()
                .MockIBD()
                .AddRPC()
                .Build();
        }
    }
}
