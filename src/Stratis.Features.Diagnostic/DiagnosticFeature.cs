using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Diagnostic.Controllers;
using Stratis.Features.Diagnostic.PeerDiagnostic;

namespace Stratis.Features.Diagnostic
{
    /// <summary>
    /// Feature for diagnostic purpose that allow to have insights about internal details of the fullnode while it's running.
    /// <para>In order to collect internal details, this feature makes use of Signals to register to internal events published
    /// by the full node and uses reflection whenever it needs to access information not meant to be publicly exposed.</para>
    /// <para>It exposes <see cref="DiagnosticController"/>, an API controller that allow to query for information using the API feature, when available.</para>
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
    public class DiagnosticFeature : FullNodeFeature
    {
        private readonly ISignals signals;
        private readonly DiagnosticSettings diagnosticSettings;
        private readonly PeerStatisticsCollector peerStatisticsCollector;

        public DiagnosticFeature(ISignals signals, DiagnosticSettings diagnosticSettings, PeerStatisticsCollector peerStatisticsCollector)
        {
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.diagnosticSettings = Guard.NotNull(diagnosticSettings, nameof(diagnosticSettings));
            this.peerStatisticsCollector = Guard.NotNull(peerStatisticsCollector, nameof(peerStatisticsCollector));
        }

        public override Task InitializeAsync()
        {
            this.peerStatisticsCollector.Initialize();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            DiagnosticSettings.PrintHelp(network);
        }

        public override void Dispose()
        {
            this.peerStatisticsCollector.Dispose();
        }
    }

    public static class DiagnosticFeatureExtension
    {
        public static IFullNodeBuilder UseDiagnosticFeature(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<DiagnosticFeature>("diagnostic");

            fullNodeBuilder.ConfigureFeature(features =>
                features
                .AddFeature<DiagnosticFeature>()
                .FeatureServices(services => services
                    .AddSingleton<DiagnosticController>()
                    .AddSingleton<PeerStatisticsCollector>()
                    .AddSingleton<DiagnosticSettings>()
                )
            );

            return fullNodeBuilder;
        }
    }
}
