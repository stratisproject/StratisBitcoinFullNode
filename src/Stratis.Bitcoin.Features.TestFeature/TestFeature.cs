using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.TestFeature.Controllers;

namespace Stratis.Bitcoin.Features.TestFeature
{
    /// <summary>
    /// Test Feature
    /// </summary>
    public class TestFeature : FullNodeFeature
    {

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public TestFeature(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("TestFeature initialized");
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderTestFeatureExtension
    {
        public static IFullNodeBuilder UseTestFeature(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<TestFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<TestFeatureController>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}
