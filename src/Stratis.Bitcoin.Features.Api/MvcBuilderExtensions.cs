using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Controllers;

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

            builder.AddApplicationPart(typeof(Controllers.NodeController).Assembly);
            builder.AddControllersAsServices();
            return builder;
        }
    }
}
