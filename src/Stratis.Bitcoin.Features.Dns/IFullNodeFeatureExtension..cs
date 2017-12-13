using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class IFullNodeFeatureExtension
    {
        /// <summary>
        /// Configures the Dns. 
        /// </summary>
        /// <param name="fullNodeBuilder">Full node builder used to configure the feature.</param>
        /// <returns>The full node builder with the Dns feature configured.</returns>
        public static IFullNodeBuilder UseDns(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<DnsFeature>("dns");
            
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<DnsFeature>()
                .FeatureServices(services => services.AddSingleton(fullNodeBuilder));
            });
           
            return fullNodeBuilder;
        }
    }
}
