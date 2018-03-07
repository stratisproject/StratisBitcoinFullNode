using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.SmartContractsApi.Controllers;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.SmartContractsApi
{
    public class SmartContractsApiFeature : FullNodeFeature
    {
        private readonly ILogger logger;

        public SmartContractsApiFeature(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("Smart Contract Wallet Feature Injected.");
        }

    }

    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder AddSmartContractsApi(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features.AddFeature<SmartContractsApiFeature>()
                .DependOn<WalletFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<SmartContractsController>();
                });
            });
            return fullNodeBuilder;
        }
    }
}
