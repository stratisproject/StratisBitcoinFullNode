﻿using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace Stratis.PoAChainD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Network network = new PoANetwork();

                var nodeSettings = new NodeSettings(args: args, network: network);

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePoAConsensus()
                    .UseMempool()
                    .UseWallet()
                    .UseApi()
                    .UseApps()
                    .AddRPC()
                    .Build();

                if (node != null)
                {
                    await node.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem running the node. Details: '{0}'", ex.ToString());
            }
        }
    }
}
