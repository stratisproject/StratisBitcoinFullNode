using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisSmartContractNode : NodeRunner
    {
        public StratisSmartContractNode(string dataDir)
            : base(dataDir)
        {
            this.Network = new SmartContractsRegTest();
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UsePowConsensus()
                .UseMempool()
                .AddMining()
                .UseWallet()
                .AddRPC()
                .MockIBD()
                .AddSmartContracts()
                .UseReflectionExecutor()
                .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}