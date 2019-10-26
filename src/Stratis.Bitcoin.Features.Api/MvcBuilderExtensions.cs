using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.Api
{
    public static class MvcBuilderExtensions
    {
        /// <summary>
        /// Identifies the assemblies to include for controller discovery.
        /// </summary>
        /// <param name="builder">The builder</param>
        /// <param name="features">The selected features to include the assemblies of.</param>
        /// <param name="serviceCollection">The full node services.</param>
        /// <returns>The Mvc builder</returns>
        public static IMvcBuilder AddControllers(this IMvcBuilder builder, IEnumerable<IFullNodeFeature> features, IServiceCollection serviceCollection)
        {
            // Detect explicitly registered transient controllers.
            // This supports the scenario where a controller is registered by a different feature than the one it belongs to.
            List<Type> explicitControllers = serviceCollection
                .Where(s => s.Lifetime == ServiceLifetime.Transient)
                .Select(s => s.ServiceType)
                .Where(t => typeof(Controller).IsAssignableFrom(t))
                .ToList();

            // The features assemblies plus assemblies of explicitly registered (foreign) controllers.
            IEnumerable<Assembly> assemblies = features
                .Select(f => f.GetType().Assembly)
                .Concat(explicitControllers.Select(c => c.Assembly))
                .Distinct();

            foreach (Assembly assembly in assemblies)
            {
                builder.AddApplicationPart(assembly);
            }

            return builder;
        }
    }
}
