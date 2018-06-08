using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Builder
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderNodeSettingsExtension
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
            nodeBuilder.Network = nodeSettings.Network;

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
            return builder.UseNodeSettings(NodeSettings.Default(builder.Network));
        }
    }
}
