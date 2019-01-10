using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class StratisSmartContractNode : NodeRunner
    {
        public StratisSmartContractNode(string dataDir, Network network)
            : base(dataDir, null)
        {
            this.Network = network;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            IFullNodeBuilder builder = NetworkNodes.GetPoWSmartContractNodeBuilder(settings);
            this.FullNode = (FullNode) builder.MockIBD()
                .UseTestChainedHeaderTree()
                .Build();
        }
    }
}