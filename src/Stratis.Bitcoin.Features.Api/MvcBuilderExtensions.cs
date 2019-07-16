using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// <returns>The Mvc builder</returns>
        public static IMvcBuilder AddControllers(this IMvcBuilder builder, IEnumerable<IFullNodeFeature> features)
        {
            // The required assemblies including those of the selected features.
            IEnumerable<Assembly> assemblies = features
                .Select(f => f.GetType().Assembly)
                .Append(typeof(Controllers.NodeController).Assembly)
                .Distinct();

            foreach (Assembly assembly in assemblies)
            {
                builder.AddApplicationPart(assembly);
            }

            return builder;
        }
    }
}
