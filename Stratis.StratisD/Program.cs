﻿using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System.Linq;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.StratisD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (NodeSettings.PrintHelp(args, Network.StratisMain))
            {
                // NOTE: Add this if .AddRPC is added below
                // RPCSettings.PrintHelp(Network.StratisMain);
                return;
            }

            Network network = args.Contains("-testnet") ? Network.StratisTest : Network.StratisMain;
            NodeSettings nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);

            // NOTES: running BTC and STRAT side by side is not possible yet as the flags for serialization are static

            var node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseStratisConsensus()
                .UseBlockStore()
                .UseMempool()
                .UseWallet()
                .AddPowPosMining()
                .AddRPC()
                .Build();

            node.Run();
        }
    }
}