using System;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.RPC;
using Stratis.LocalSmartContracts.Networks;

class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var nodeSettings = new NodeSettings(network: new LocalSmartContracts(), protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: args)
            {
                MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
            };

            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .UseMempool()
                .AddSmartContracts()
                .UseSmartContractPoAConsensus()
                .UseSmartContractPoAMining()
                .UseSmartContractWallet()
                .UseReflectionExecutor()
                .UseApi()
                .AddRPC()
                .Build();

            if (node != null)
                await node.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
        }
    }
}