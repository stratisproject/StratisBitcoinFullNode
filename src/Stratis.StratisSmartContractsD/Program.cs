﻿using System;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace Stratis.StratisSmartContractsD
{
    class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                Network network = NetworksContainer.Register(new SmartContractsTest());
                NodeSettings nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, "StratisSC", args: args);

                Bitcoin.IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UseSmartContractConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddMining()
                    .UseApi()
                    .AddRPC()
                    .AddSmartContracts()
                    .UseReflectionExecutor()
                    .Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }
    }
}