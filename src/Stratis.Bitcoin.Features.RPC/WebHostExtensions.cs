using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.RPC.ModelBinders;

namespace Stratis.Bitcoin.Features.RPC
{
    public static class WebHostExtensions
    {
        public static IWebHostBuilder ForFullNode(this IWebHostBuilder hostBuilder, FullNode fullNode)
        {
            hostBuilder.ConfigureServices(s =>
            {
                IMvcCoreBuilder mvcBuilder = s.AddMvcCore(o =>
                {
                    o.ModelBinderProviders.Insert(0, new DestinationModelBinder());
                    o.ModelBinderProviders.Insert(0, new MoneyModelBinder());
                });

                // Include all feature assemblies for action discovery otherwise RPC actions will not execute
                // https://stackoverflow.com/questions/37725934/asp-net-core-mvc-controllers-in-separate-assembly
                foreach (Assembly assembly in fullNode.Services.Features.OfType<FullNodeFeature>().Select(x => x.GetType().GetTypeInfo().Assembly).Distinct())
                    mvcBuilder.AddApplicationPart(assembly);
            });

            return hostBuilder;
        }
    }
}
