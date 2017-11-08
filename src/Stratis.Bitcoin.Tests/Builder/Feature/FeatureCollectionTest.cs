using System;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
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

            Assert.Single(collection.FeatureRegistrations);
            Assert.Equal(typeof(FeatureCollectionFullNodeFeature), collection.FeatureRegistrations[0].FeatureType);
        }

        [Fact]
        public void AddFeatureAlreadyInCollectionThrowsException()
        {
            Assert.Throws<ArgumentException>(() =>
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

            public void ValidateDependencies(IFullNodeServiceProvider services)
            {
                throw new NotImplementedException();
            }
        }
    }
}
