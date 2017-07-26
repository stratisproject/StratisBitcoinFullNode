using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Features.RPC.ModelBinders;

namespace Stratis.Bitcoin.Features.RPC
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
