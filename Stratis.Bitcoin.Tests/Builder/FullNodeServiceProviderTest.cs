using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.Builder
{
    [TestClass]
	public class FullNodeServiceProviderTest
	{
		private Mock<IServiceProvider> serviceProvider;

        [TestInitialize]
		public void Initialize()
		{
			this.serviceProvider = new Mock<IServiceProvider>();
			this.serviceProvider.Setup(c => c.GetService(typeof(TestFeatureStub)))
				.Returns(new TestFeatureStub());
			this.serviceProvider.Setup(c => c.GetService(typeof(TestFeatureStub2)))
				.Returns(new TestFeatureStub2());
		}

		[TestMethod]
		public void FeaturesReturnsFullNodeFeaturesFromServiceProvider()
		{
			var types = new List<Type> {
				typeof(TestFeatureStub),
				typeof(TestFeatureStub2)
			};

			var fullnodeServiceProvider = new FullNodeServiceProvider(this.serviceProvider.Object, types);
			var result = fullnodeServiceProvider.Features.ToList();

			Assert.AreEqual(2, result.Count);			
			Assert.AreEqual(typeof(TestFeatureStub), result[0].GetType());
			Assert.AreEqual(typeof(TestFeatureStub2), result[1].GetType());
		}

		[TestMethod]
		public void FeaturesReturnsInGivenOrder()
		{
			var types = new List<Type> {
				typeof(TestFeatureStub2),
				typeof(TestFeatureStub)

			};

			var fullnodeServiceProvider = new FullNodeServiceProvider(this.serviceProvider.Object, types);
			var result = fullnodeServiceProvider.Features.ToList();

			Assert.AreEqual(2, result.Count);
			Assert.AreEqual(typeof(TestFeatureStub2), result[0].GetType());
			Assert.AreEqual(typeof(TestFeatureStub), result[1].GetType());
		}

		private class TestFeatureStub : IFullNodeFeature
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

		private class TestFeatureStub2 : IFullNodeFeature
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
