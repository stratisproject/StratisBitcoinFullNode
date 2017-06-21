using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.Builder.Feature
{
    [TestClass]
    public class FeatureRegistrationTest
    {
		[TestMethod]
		public void FeatureServicesAddServiceCollectionToDelegates()
		{
			var collection = new ServiceCollection();			
			var registration = new FeatureRegistration<FeatureRegistrationFullNodeFeature>();
			
			registration.FeatureServices(d => { d.AddSingleton<FeatureCollection>(); });

			Assert.AreEqual(typeof(FeatureRegistrationFullNodeFeature), registration.FeatureType);
			Assert.AreEqual(1, registration.ConfigureServicesDelegates.Count);
			registration.ConfigureServicesDelegates[0].Invoke(collection);
			var descriptors = collection as IList<ServiceDescriptor>;
			Assert.AreEqual(1, descriptors.Count);
			Assert.AreEqual(typeof(FeatureCollection), descriptors[0].ImplementationType);
			Assert.AreEqual(ServiceLifetime.Singleton, descriptors[0].Lifetime);
		}

		[TestMethod]
		public void UseStartupSetsFeatureStartupType()
		{			
			var registration = new FeatureRegistration<FeatureRegistrationFullNodeFeature>();
			Assert.AreEqual(null, registration.FeatureStartupType);

			registration.UseStartup<ServiceCollection>();

			Assert.AreEqual(typeof(ServiceCollection), registration.FeatureStartupType);		
		}

		[TestMethod]
		public void BuildFeatureWithoutFeatureStartupTypeBootstrapsStartup()
		{
			var collection = new ServiceCollection();
			var registration = new FeatureRegistration<FeatureRegistrationFullNodeFeature>();
			registration.FeatureServices(d => { d.AddSingleton<FeatureCollection>(); });

			registration.BuildFeature(collection);

			var descriptors = collection as IList<ServiceDescriptor>;
			Assert.AreEqual(3, descriptors.Count);			
			Assert.AreEqual(typeof(FeatureRegistrationFullNodeFeature), descriptors[0].ImplementationType);
			Assert.AreEqual(ServiceLifetime.Singleton, descriptors[0].Lifetime);
			Assert.AreEqual(typeof(IFullNodeFeature), descriptors[1].ServiceType);
			Assert.IsNotNull(descriptors[1].ImplementationFactory);
			Assert.AreEqual(ServiceLifetime.Singleton, descriptors[1].Lifetime);
			Assert.AreEqual(typeof(FeatureCollection), descriptors[2].ImplementationType);
			Assert.AreEqual(ServiceLifetime.Singleton, descriptors[2].Lifetime);
		}

		[TestMethod]
		public void BuildFeatureWithFeatureStartupTypeBootstrapsStartupAndInvokesStartupWithCollection()
		{						
			var collection = new ServiceCollection();
			var registration = new FeatureRegistration<FeatureRegistrationFullNodeFeature>();
			registration.FeatureServices(d => { d.AddSingleton<FeatureCollection>(); });
			registration.UseStartup<FeatureStartup>();

			registration.BuildFeature(collection);
		}

		[TestMethod]
		public void BuildFeatureWithFeatureStartupNotHavingStaticConfigureServicesMethodDoesNotCrash()
		{
			var collection = new ServiceCollection();
			var registration = new FeatureRegistration<FeatureRegistrationFullNodeFeature>();
			registration.FeatureServices(d => { d.AddSingleton<FeatureCollection>(); });
			registration.UseStartup<FeatureNonStaticStartup>();

			registration.BuildFeature(collection);
		}

		private class FeatureNonStaticStartup
		{
			public void ConfigureServices(IServiceCollection services)
			{			
			}
		}

		private class FeatureStartup
		{
			public static void ConfigureServices(IServiceCollection services)
			{
				var descriptors = services as IList<ServiceDescriptor>;
				Assert.AreEqual(3, descriptors.Count);
				Assert.AreEqual(typeof(FeatureRegistrationFullNodeFeature), descriptors[0].ImplementationType);
				Assert.AreEqual(ServiceLifetime.Singleton, descriptors[0].Lifetime);
				Assert.AreEqual(typeof(IFullNodeFeature), descriptors[1].ServiceType);
				Assert.IsNotNull(descriptors[1].ImplementationFactory);
				Assert.AreEqual(ServiceLifetime.Singleton, descriptors[1].Lifetime);
				Assert.AreEqual(typeof(FeatureCollection), descriptors[2].ImplementationType);
				Assert.AreEqual(ServiceLifetime.Singleton, descriptors[2].Lifetime);
			}
		}

		private class FeatureRegistrationFullNodeFeature : IFullNodeFeature
		{
			public void Start()
			{
				throw new NotImplementedException();
			}

			public void Stop()
			{
				throw new NotImplementedException();
			}
		}
	}
}

