using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Miner;
using Stratis.Bitcoin.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC
{
    public static class WebHostExtensions
    {
        public static IWebHostBuilder ForFullNode(this IWebHostBuilder hostBuilder, FullNode fullNode)
        {
            hostBuilder.ConfigureServices(s =>
            {
                s.AddSingleton(fullNode);
                s.AddSingleton(fullNode as Builder.IFullNode);
                s.AddSingleton(fullNode.Network);
                s.AddSingleton(fullNode.Settings);
                s.AddSingleton(fullNode.ConsensusLoop);
                s.AddSingleton(fullNode.ConsensusLoop?.Validator);
                s.AddSingleton(fullNode.Chain);
                s.AddSingleton(fullNode.ChainBehaviorState);
                s.AddSingleton(fullNode.BlockStoreManager);
                s.AddSingleton(fullNode.MempoolManager);
                s.AddSingleton(fullNode.ConnectionManager);
                s.AddSingleton(fullNode.Services.ServiceProvider.GetService<IWalletManager>());
                var pow = fullNode.Services.ServiceProvider.GetService<PowMining>();
                if(pow != null)
                    s.AddSingleton(pow);
            });
            return hostBuilder;
        }
    }
}
