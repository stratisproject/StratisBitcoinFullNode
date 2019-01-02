using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public static class FullNodeTestBuilderExtension
    {
        /// <summary>
        /// Substitute the <see cref="IDateTimeProvider"/> for a given feature.
        /// </summary>
        /// <typeparam name="T">The feature to substitute the provider for.</typeparam>
        public static IFullNodeBuilder OverrideDateTimeProviderFor<T>(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                IFeatureRegistration feature = features.FeatureRegistrations.FirstOrDefault(f => f.FeatureType == typeof(T));
                if (feature != null)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor service = services.FirstOrDefault(s => s.ServiceType == typeof(IDateTimeProvider));
                        if (service != null)
                            services.Remove(service);

                        services.AddSingleton<IDateTimeProvider, GenerateCoinsFastDateTimeProvider>();
                    });
                }
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder MockIBD(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        // Get default IBD implementation and replace it with the mock.
                        ServiceDescriptor ibdService = services.FirstOrDefault(x => x.ServiceType == typeof(IInitialBlockDownloadState));

                        if (ibdService != null)
                        {
                            services.Remove(ibdService);
                            services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadStateMock>();
                        }
                    });
                }
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseTestChainedHeaderTree(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        // Get default CHT implementation and replace it with the test implementation.
                        ServiceDescriptor cht = services.FirstOrDefault(x => x.ServiceType == typeof(IChainedHeaderTree));

                        services.Remove(cht);
                        services.AddSingleton<IChainedHeaderTree, TestChainedHeaderTree>()
                            .AddSingleton<TestChainedHeaderTree>(provider => provider.GetService<IChainedHeaderTree>() as TestChainedHeaderTree);
                    });
                }
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder ReplaceTimeProvider(this IFullNodeBuilder fullNodeBuilder, IDateTimeProvider timeProvider)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor defaultProivider = services.FirstOrDefault(x => x.ServiceType == typeof(IDateTimeProvider));

                        services.Remove(defaultProivider);
                        services.AddSingleton<IDateTimeProvider>(provider => timeProvider);
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}