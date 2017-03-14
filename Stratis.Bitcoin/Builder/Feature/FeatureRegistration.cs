using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Builder.Feature
{
	public class FeatureRegistration
	{
		public readonly List<Action<IServiceCollection>> ConfigureServicesDelegates;

		public FeatureRegistration(Type featureType)
		{
			ConfigureServicesDelegates = new List<Action<IServiceCollection>>();
			FeatureType = featureType;
		}

		public Type FeatureType { get; }

		public Type FeatureStartupType { get; private set; }

		public FeatureRegistration FeatureServices(Action<IServiceCollection> configureServices)
		{
			if (configureServices == null)
				throw new ArgumentNullException(nameof(configureServices));

			ConfigureServicesDelegates.Add(configureServices);

			return this;
		}

		public FeatureRegistration UseStartup<TStartup>()
		{
			FeatureStartupType = typeof(TStartup);
			return this;
		}

		public void BuildFeature(IServiceCollection serviceCollection)
		{
			// features can only be singletons
			serviceCollection
				.AddSingleton(FeatureType)
				.AddSingleton(typeof(IFullNodeFeature), provider => provider.GetService(FeatureType));

			foreach (var configureServicesDelegate in ConfigureServicesDelegates)
				configureServicesDelegate(serviceCollection);

			if (FeatureStartupType != null)
				FeatureStartup(serviceCollection, FeatureStartupType);
		}

		/// <summary>
		///     A feature can use specified method to configure its services
		///     The specified method needs to have the following signature to be invoked
		///     void ConfigureServices(IServiceCollection serviceCollection)
		/// </summary>
		private void FeatureStartup(IServiceCollection serviceCollection, Type startupType)
		{
			var method = startupType.GetMethod("ConfigureServices");
			var parameters = method?.GetParameters();
			if ((parameters?.Length == 1) && (parameters.First().ParameterType == typeof(IServiceCollection)))
				method.Invoke(null, new object[] {serviceCollection});
		}
	}
}