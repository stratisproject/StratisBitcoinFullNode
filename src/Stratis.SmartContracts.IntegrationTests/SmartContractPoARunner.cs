using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class SmartContractPoARunner : NodeRunner
    {
        private readonly IDateTimeProvider dateTimeProvider;

        public SmartContractPoARunner(string dataDir, Network network, EditableTimeProvider timeProvider)
            : base(dataDir)
        {
            this.Network = network;
            this.dateTimeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .AddSmartContracts()
                .UseSmartContractPoAConsensus()
                .UseSmartContractPoAMining()
                .UseSmartContractWallet()
                .UseReflectionExecutor()
                .MockIBD()
                .Build();
        }
    }
}
