using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Builder
{
    /// <summary>
    /// Extension methods for FullNodeBuilder class.
    /// </summary>
    public static class FullNodeBuilderExtensions
    {
        /// <summary>
        /// Makes the full node builder use specific node settings.
        /// </summary>
        /// <param name="builder">Full node builder to change node settings for.</param>
        /// <param name="nodeSettings">Node settings to be used.</param>
        /// <returns>Interface to allow fluent code.</returns>
        public static IFullNodeBuilder UseNodeSettings(this IFullNodeBuilder builder, NodeSettings nodeSettings)
        {
            var nodeBuilder = builder as FullNodeBuilder;
            nodeBuilder.NodeSettings = nodeSettings;
            nodeBuilder.Network = nodeSettings.GetNetwork();

            builder.ConfigureServices(service =>
            {
                service.AddSingleton(nodeBuilder.NodeSettings);
                service.AddSingleton(nodeBuilder.Network);
            });

            return builder.UseBaseFeature();
        }

        /// <summary>
        /// Makes the full node builder use the default node settings.
        /// </summary>
        /// <param name="builder">Full node builder to change node settings for.</param>
        /// <returns>Interface to allow fluent code.</returns>
        public static IFullNodeBuilder UseDefaultNodeSettings(this IFullNodeBuilder builder)
        {
            return builder.UseNodeSettings(NodeSettings.Default());
        }

        /// <summary>
        /// Get service of type T from the System.IServiceProvider.
        /// </summary>
        /// <typeparam name="T">The type of service object to get.</typeparam>
        /// <param name="serviceProvider">The System.IServiceProvider to retrieve the service object from.</param>
        /// <returns>A service object of type T or null if there is no such service.</returns>
        /// <remarks>This extension method probably does not belong to this class/file.</remarks>
        public static T Service<T>(this IServiceProvider serviceProvider)
        {
            return (T)serviceProvider.GetService<T>();
        }
    }
}