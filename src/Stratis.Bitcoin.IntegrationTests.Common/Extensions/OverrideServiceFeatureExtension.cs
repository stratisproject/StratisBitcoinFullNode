using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public static class OverrideServiceFeatureExtension
    {
        /// <summary>
        /// Adds a feature to the node that will allow certain services to be overridden.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="serviceToOverride">Callback routine that will override a given service.</param>
        /// <typeparam name="T">The feature that the service will be replaced in.</typeparam>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder OverrideService<T>(this IFullNodeBuilder fullNodeBuilder, Action<IServiceCollection> serviceToOverride)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                IFeatureRegistration feature = features.FeatureRegistrations.FirstOrDefault(f => f.FeatureType == typeof(T));
                if (feature != null)
                {
                    feature.FeatureServices(services =>
                    {
                        serviceToOverride(services);
                    });
                }
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Replaces a service in a given feature with another implementation.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="serviceToOverride">Callback routine that will override a given service.</param>
        /// <typeparam name="T">The services to be replaced.</typeparam>
        /// <typeparam name="TFeature">The feature that the service exists in.</typeparam>
        /// <returns>The full node builder, with the replaced service.</returns>
        public static IFullNodeBuilder ReplaceService<T, TFeature>(this IFullNodeBuilder fullNodeBuilder, T serviceToOverride)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                IFeatureRegistration feature = features.FeatureRegistrations.FirstOrDefault(f => f.FeatureType == typeof(TFeature));
                if (feature != null)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor service = services.FirstOrDefault(s => s.ServiceType == typeof(T));
                        if (service != null)
                        {
                            services.Remove(service);
                            services.AddSingleton(typeof(T), serviceToOverride);
                        }
                    });
                }
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Removes an implementation from a given feature.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <typeparam name="T">The service to remove.</typeparam>
        /// <returns>The full node builder, with the service removed.</returns>
        public static IFullNodeBuilder RemoveImplementation<T>(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                IFeatureRegistration feature = features.FeatureRegistrations.FirstOrDefault(f => f.FeatureType == typeof(BaseFeature));
                if (feature != null)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor service = services.FirstOrDefault(s => s.ImplementationType == typeof(T));
                        if (service != null)
                            services.Remove(service);
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}
