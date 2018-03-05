using System;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Sidechains;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Api;

namespace Stratis.Sidechains
{
    class Program
    {
        static void Main(string[] args)
        {
	        args = new string[]{"stratis", "-testnet", "-loglevel=debug", "-addnode=127.0.0.1:1234"};

	        Network network = Network.StratisTest;
	        NodeSettings nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);
			nodeSettings.ApiUri = new Uri("http://localhost:5000");

			// NOTES: running BTC and STRAT side by side is not possible yet as the flags for serialization are static

			var node = new FullNodeBuilder()
		        .UseNodeSettings(nodeSettings)
				.UseSidechains()
		        .UseStratisConsensus()
		        .UseBlockStore()
		        .UseMempool()
		        .UseWallet()
				.UseWatchOnlyWallet()
		        .AddPowPosMining()
		        .AddRPC()
				.UseApi()
		        .Build();

	        node.Run();
		}
    }
}
