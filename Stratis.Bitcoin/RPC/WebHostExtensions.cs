using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Miner;
using Stratis.Bitcoin.RPC.ModelBinders;
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
                s.AddMvcCore(o =>
                {
                    o.ModelBinderProviders.Insert(0, new DestinationModelBinder());
                    o.ModelBinderProviders.Insert(0, new MoneyModelBinder());
                });
            });

            return hostBuilder;
        }
    }
}
