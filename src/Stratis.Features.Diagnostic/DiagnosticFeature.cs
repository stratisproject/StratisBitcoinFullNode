using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Diagnostic.Controllers;
using Stratis.Features.Diagnostic.PeerDiagnostic;
using Stratis.Features.Diagnostic.Utils;

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
        private ISignals signals;

        public DiagnosticFeature(ISignals signals, INodeStats nodeStats)
        {
            this.signals = signals;
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline);
        }

        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        private void AddInlineStats(StringBuilder builder)
        {
            builder.Append("GC.GetTotalMemory: ".PadRight(LoggingConfiguration.ColumnLength + 1));
            builder.AppendLine($"{GC.GetTotalMemory(false).SizeSuffix(2)}");
        }
    }


    public static class DiagnosticFeatureExtension
    {
        public static IFullNodeBuilder UseCustomFeature(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<DiagnosticFeature>("diagnostic");

            fullNodeBuilder.ConfigureFeature(features =>
                features
                .AddFeature<DiagnosticFeature>()
                .FeatureServices(services => services
                    .AddSingleton<DiagnosticController>()
                    .AddSingleton<PeerDiagnosticCollector>()
                )
            );

            return fullNodeBuilder;
        }
    }
}
