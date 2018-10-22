using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools
{
    public static class FullNodePoATestBuilderExtension
    {
        public static IFullNodeBuilder AddFastMiningCapability(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor defaultProivider = services.FirstOrDefault(x => x.ServiceType == typeof(PoAMiner));

                        services.Remove(defaultProivider);
                        services.AddSingleton<IPoAMiner, TestPoAMiner>();
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}
