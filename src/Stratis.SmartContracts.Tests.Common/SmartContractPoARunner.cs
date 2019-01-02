using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Tests.Common.MockChain;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class SmartContractPoARunner : NodeRunner
    {
        private readonly IDateTimeProvider dateTimeProvider;

        public SmartContractPoARunner(string dataDir, Network network, TargetSpacingDateTimeProvider timeProvider)
            : base(dataDir, null)
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
                .ReplaceTimeProvider(this.dateTimeProvider)
                .MockIBD()
                .Build();
        }
    }
}
