﻿using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisBitcoinPowApiRunner : NodeRunner
    {
        public StratisBitcoinPowApiRunner(string dataDir)
            : base(dataDir)
        {
            this.Network = Networks.RegTest;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UsePowConsensus()
                .UseMempool()
                .AddMining()
                .UseWallet()
                .AddRPC()
                .UseApi()
                .MockIBD()
                .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}