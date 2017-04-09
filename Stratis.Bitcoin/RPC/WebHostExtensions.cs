using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
            });
            return hostBuilder;
        }
    }
}
