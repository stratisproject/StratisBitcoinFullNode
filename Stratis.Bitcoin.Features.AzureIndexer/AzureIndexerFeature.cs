using System;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.AzureIndexer.Tests")]

namespace Stratis.Bitcoin.Features.AzureIndexer
{
    public class AzureIndexerFeature: FullNodeFeature, INodeStats
    {
        protected readonly AzureIndexerLoop indexerLoop;
        protected readonly NodeSettings nodeSettings;
        protected readonly AzureIndexerSettings indexerSettings;
        protected readonly INodeLifetime nodeLifetime;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        protected readonly string name;

        public AzureIndexerFeature(
            AzureIndexerLoop azureIndexerLoop,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            AzureIndexerSettings indexerSettings,
            string name = "AzureIndexer")
        {
            this.name = name;
            this.indexerLoop = azureIndexerLoop;
            this.nodeLifetime = nodeLifetime;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            indexerSettings.Load(nodeSettings);
            this.indexerSettings = indexerSettings;
        }

        public void AddNodeStats(StringBuilder benchLogs)
        {
            var highestBlock = this.indexerLoop.StoreTip;

            if (highestBlock != null)
                benchLogs.AppendLine($"{this.name}.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                    highestBlock.Height.ToString().PadRight(8) +
                    $" {this.name}.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                    highestBlock.HashBlock);
        }

        public override void Start()
        {
            this.logger.LogInformation("Starting {0}...", this.name);
            this.indexerLoop.Initialize();         
            this.logger.LogTrace("(-)");
        }

        public override void Stop()
        {
            this.logger.LogInformation("Stopping {0}...", this.name);
            this.indexerLoop.Shutdown();
            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseAzureIndexer(this IFullNodeBuilder fullNodeBuilder, Action<AzureIndexerSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<AzureIndexerFeature>("azindex");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<AzureIndexerFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<BlockStore.IBlockRepository, BlockStore.BlockRepository>();
                    services.AddSingleton<ConnectionManager>();
                    services.AddSingleton<AzureIndexerLoop>();
                    services.AddSingleton<AzureIndexerSettings>(new AzureIndexerSettings(setup));
                });
            });

            return fullNodeBuilder;
        }
    }
}