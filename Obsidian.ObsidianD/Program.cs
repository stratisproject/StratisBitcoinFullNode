using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.Coin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.ObsidianD
{
    public class Program
    {
        public static void Main(string[] args)
        {
	        var fix= Network.Main; // execute static constructor of Network before everything else, or hashes will get mixed up.

	        var network = !args.Contains("-testnet") 
				? ObsidianNetworks.RegisterMain() 
				: ObsidianNetworks.RegisteTest();

	        if (NodeSettings.PrintHelp(args, network))
		        return;
		
            var nodeSettings = NodeSettings.FromArguments(args, "obsidian", network, ProtocolVersion.ALT_PROTOCOL_VERSION);

            var node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseStratisConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddPowPosMining()
	            .AddRPC()
				.Build();

            Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(t =>
            {
                TryStartPowMiner(args, node);
                //TryStartPosMiner(args, node);
            });

            node.Run();
        }

        private static void TryStartPowMiner(string[] args, IFullNode node)
        {
            // mining can be called from either RPC or on start
            // to manage the on strat we need to get an address to the mining code
            var mine = args.FirstOrDefault(a => a.Contains("mine="));
            if (mine != null)
            {
                // get the address to mine to
                var addres = mine.Replace("mine=", string.Empty);
                var pubkey = BitcoinAddress.Create(addres, node.Network);
                node.Services.ServiceProvider.GetService<PowMining>().Mine(pubkey.ScriptPubKey);
            }
        }

        static void TryStartPosMiner(string[] args, IFullNode node)
        {
            // mining can be called from either RPC or on start
            // to manage the on strat we need to get an address to the mining code
            var mine = args.FirstOrDefault(a => a.Contains("mine="));
            var walletNameArg = args.FirstOrDefault(a => a.Contains("walletname="));
            var walletPasswordArg = args.FirstOrDefault(a => a.Contains("walletpassword="));

            if (mine != null && walletNameArg != null && walletPasswordArg != null)
            {
                var walletName = walletNameArg.Replace("walletname=", string.Empty);
                var walletPassword = walletPasswordArg.Replace("walletpassword=", string.Empty);

                node.Services.ServiceProvider.GetService<PosMinting>().Mine(new PosMinting.WalletSecret()
                {
                    WalletPassword = walletPassword,
                    WalletName = walletName
                });
            }
        }
    }
}