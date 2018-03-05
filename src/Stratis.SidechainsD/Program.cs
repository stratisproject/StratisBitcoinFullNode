using System;
using System.Threading.Tasks;
using NBitcoin;
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
using Stratis.Bitcoin.Utilities;

namespace Stratis.SidechainD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            FullAsync(args).Wait();
        }

        public static async Task FullAsync(string[] args)
        {
            try
            {
                var sidechainIdentifier = SidechainIdentifier.CreateFromArgs(args);
                NodeSettings nodeSettings = new NodeSettings(Network.SidechainMain, ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                Action<MinerSettings> act = settings => GetMinerSettings();

                var node = new FullNodeBuilder()
                      .UseNodeSettings(nodeSettings)
                      .UsePosConsensus()
                      .UseBlockStore()
                      .UseMempool()
                      .UseWallet()
                      .AddPowPosMining(act)
                      .UseApi()
                      .AddRPC()
                      .Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        private static MinerSettings GetMinerSettings()
        {
            //testnet
            //qedcGY2KkpZiQ3JsMTBcPd8JwAXM4vz9Nb
            //regtest
            //SPgEYdqmrbkLBR3dAhUgsRDvyHZyhpvPFs
            //mainnet
            //RaYuu3wJJ2cJkPtfb6RCbsF4aQgYrfGNqR
            var minerSetting = new MinerSettings();
            minerSetting.MineAddress = "RaYuu3wJJ2cJkPtfb6RCbsF4aQgYrfGNqR";
            return minerSetting;
        }
    }
}