using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder.Feature
{
	public interface IFeatureRegistration
	{
		Type FeatureStartupType { get; }
		Type FeatureType { get; }

		void BuildFeature(IServiceCollection serviceCollection);
		IFeatureRegistration FeatureServices(Action<IServiceCollection> configureServices);
		IFeatureRegistration UseStartup<TStartup>();
	}

	public class FeatureRegistration<TImplementation> : IFeatureRegistration where TImplementation : class, IFullNodeFeature
	{
		public readonly List<Action<IServiceCollection>> ConfigureServicesDelegates;

		public FeatureRegistration()
		{		
			this.ConfigureServicesDelegates = new List<Action<IServiceCollection>>();
			this.FeatureType = typeof(TImplementation);
		}	

		public Type FeatureType { get; private set; }

		public Type FeatureStartupType { get; private set; }

		public IFeatureRegistration FeatureServices(Action<IServiceCollection> configureServices)
		{
			Guard.NotNull(configureServices, nameof(configureServices));

			this.ConfigureServicesDelegates.Add(configureServices);

			return this;
		}

		public IFeatureRegistration UseStartup<TStartup>()
		{
            this.FeatureStartupType = typeof(TStartup);
			return this;
		}

		public void BuildFeature(IServiceCollection serviceCollection)
		{
			Guard.NotNull(serviceCollection, nameof(serviceCollection));

			// features can only be singletons
			serviceCollection
				.AddSingleton(this.FeatureType)
				.AddSingleton(typeof(IFullNodeFeature), provider => provider.GetService(this.FeatureType));

			foreach (var configureServicesDelegate in this.ConfigureServicesDelegates)
				configureServicesDelegate(serviceCollection);

			if (this.FeatureStartupType != null)
				FeatureStartup(serviceCollection, this.FeatureStartupType);
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
			if (method != null && method.IsStatic && (parameters?.Length == 1) && (parameters.First().ParameterType == typeof(IServiceCollection)))
				method.Invoke(null, new object[] { serviceCollection });
		}
	}
}