using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.Builder.Feature
{
    [TestClass]
    public class FeatureCollectionTest
    {
		[TestMethod]
		public void AddToCollectionReturnsOfGivenType()
		{
			var collection = new FeatureCollection();

			collection.AddFeature<FeatureCollectionFullNodeFeature>();

			Assert.AreEqual(1, collection.FeatureRegistrations.Count);
			Assert.AreEqual(typeof(FeatureCollectionFullNodeFeature), collection.FeatureRegistrations[0].FeatureType);
		}

		[TestMethod]
        [ExpectedException(typeof(ArgumentException))]
		public void AddFeatureAlreadyInCollectionThrowsException()
		{			
			var collection = new FeatureCollection();

			collection.AddFeature<FeatureCollectionFullNodeFeature>();
			collection.AddFeature<FeatureCollectionFullNodeFeature>();		
		}

		private class FeatureCollectionFullNodeFeature : IFullNodeFeature
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
