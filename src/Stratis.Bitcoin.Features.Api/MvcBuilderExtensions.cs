using System;
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
            // Adds the assembles containing our features.
            var featureAssemblies = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                .Where(x => typeof(FeatureController).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(x => x.Assembly)
                .ToList();

            foreach (Assembly assembly in featureAssemblies)
            {
                builder.AddApplicationPart(assembly);
            }

            builder.AddApplicationPart(typeof(Controllers.NodeController).Assembly);
            builder.AddControllersAsServices();
            return builder;
        }
    }
}
