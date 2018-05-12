using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Dashboard.Controllers; 

namespace Stratis.Bitcoin.Features.Dashboard
{
    /// <summary>
    /// Dashboard Feature
    /// </summary>
	public class Dashboard : FullNodeFeature 
    {

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger; 

		public Dashboard(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName); 
        }

        public override void Initialize()
        {
			this.logger.LogInformation("Dashboard initialized");
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
	public static class FullNodeBuilderDashboardExtension
    {
		public static IFullNodeBuilder UseDashboard(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
					.AddFeature<Dashboard>()
                    .DependOn<ApiFeature>() // API endpoints are going to be required to display data and make method calls from the dashboard
                    .FeatureServices(services =>
                    {
					services.AddSingleton<DashboardController>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}