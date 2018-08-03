using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.Apps;

namespace Stratis.Bitcoin.Features.Api
{
    public static class MvcBuilderExtensions
    {
        /// <summary>
        /// Finds all the types that are <see cref="Controller"/> or <see cref="FeatureController"/>and add them to the Api as services.
        /// </summary>
        /// <param name="builder">The builder</param>
        /// <param name="services">The services to look into</param>
        /// <returns>The Mvc builder</returns>
        public static IMvcBuilder AddControllers(this IMvcBuilder builder, IServiceCollection services)
        {
            // Adds Controllers with API endpoints
            System.Collections.Generic.IEnumerable<ServiceDescriptor> controllerTypes = services.Where(s => s.ServiceType.GetTypeInfo().BaseType == typeof(Controller));

            Network network = services.BuildServiceProvider().GetService<Network>();
            if (network.Consensus.IsProofOfStake)
            {
                // Filters out controllers flagged by HideWhenProofOfStake Attribute.
                controllerTypes = controllerTypes.Where(u => u.ServiceType.GetCustomAttributes(typeof(HideWhenProofOfStakeAttribute), true).Length == 0);
            }
            else
            {
                // Filters out controllers flagged by HideWhenProofOfWork Attribute.
                controllerTypes = controllerTypes.Where(u => u.ServiceType.GetCustomAttributes(typeof(HideWhenProofOfWorkAttribute), true).Length == 0);
            }

            foreach (ServiceDescriptor controllerType in controllerTypes)
            {
                builder.AddApplicationPart(controllerType.ServiceType.GetTypeInfo().Assembly);
            }

            // Adds FeatureControllers with API endpoints.
            System.Collections.Generic.IEnumerable<ServiceDescriptor> featureControllerTypes = services.Where(s => s.ServiceType.GetTypeInfo().BaseType == typeof(FeatureController));
            foreach (ServiceDescriptor featureControllerType in featureControllerTypes)
            {
                builder.AddApplicationPart(featureControllerType.ServiceType.GetTypeInfo().Assembly);
            }

            builder.AddApplicationPart(typeof(NodeController).Assembly);
            builder.AddApplicationPart(typeof(DashboardController).Assembly);
            builder.AddApplicationPart(typeof(AppsController).Assembly);
            return builder;
        }
    }
}
