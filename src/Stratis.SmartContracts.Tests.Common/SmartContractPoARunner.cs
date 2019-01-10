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
using Stratis.SmartContracts.Networks;
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
            IFullNodeBuilder builder = NetworkNodes.GetPoASmartContractNodeBuilder(settings);
            this.FullNode = (FullNode)builder.MockIBD()
                .ReplaceTimeProvider(this.dateTimeProvider)
                .Build();
        }
    }
}
