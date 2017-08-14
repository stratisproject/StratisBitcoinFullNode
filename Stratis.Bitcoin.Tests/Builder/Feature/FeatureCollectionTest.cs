using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder.Feature
{
    public class FeatureCollectionTest
    {
        [Fact]
        public void AddToCollectionReturnsOfGivenType()
        {
            var collection = new FeatureCollection();

            collection.AddFeature<FeatureCollectionFullNodeFeature>();

            Assert.Equal(1, collection.FeatureRegistrations.Count);
            Assert.Equal(typeof(FeatureCollectionFullNodeFeature), collection.FeatureRegistrations[0].FeatureType);
        }

        [Fact]
        public void AddFeatureAlreadyInCollectionThrowsException()
        {
            Assert.Throws(typeof(ArgumentException), () =>
            {
                var collection = new FeatureCollection();

                collection.AddFeature<FeatureCollectionFullNodeFeature>();
                collection.AddFeature<FeatureCollectionFullNodeFeature>();
            });
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

            public void ValidateDependencies(IEnumerable<IFullNodeFeature> features)
            {
                throw new NotImplementedException();
            }
        }
    }
}
