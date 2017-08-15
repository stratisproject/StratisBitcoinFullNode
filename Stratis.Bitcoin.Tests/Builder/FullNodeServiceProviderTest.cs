using Moq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder
{
    public class FullNodeServiceProviderTest
    {
        private Mock<IServiceProvider> serviceProvider;

        public FullNodeServiceProviderTest()
        {
            this.serviceProvider = new Mock<IServiceProvider>();
            this.serviceProvider.Setup(c => c.GetService(typeof(TestFeatureStub)))
                .Returns(new TestFeatureStub());
            this.serviceProvider.Setup(c => c.GetService(typeof(TestFeatureStub2)))
                .Returns(new TestFeatureStub2());
        }

        [Fact]
        public void FeaturesReturnsFullNodeFeaturesFromServiceProvider()
        {
            var types = new List<Type> {
                typeof(TestFeatureStub),
                typeof(TestFeatureStub2)
            };

            var fullnodeServiceProvider = new FullNodeServiceProvider(this.serviceProvider.Object, types);
            var result = fullnodeServiceProvider.Features.ToList();

            Assert.Equal(2, result.Count);			
            Assert.Equal(typeof(TestFeatureStub), result[0].GetType());
            Assert.Equal(typeof(TestFeatureStub2), result[1].GetType());
        }

        [Fact]
        public void FeaturesReturnsInGivenOrder()
        {
            var types = new List<Type> {
                typeof(TestFeatureStub2),
                typeof(TestFeatureStub)

            };

            var fullnodeServiceProvider = new FullNodeServiceProvider(this.serviceProvider.Object, types);
            var result = fullnodeServiceProvider.Features.ToList();

            Assert.Equal(2, result.Count);
            Assert.Equal(typeof(TestFeatureStub2), result[0].GetType());
            Assert.Equal(typeof(TestFeatureStub), result[1].GetType());
        }

        private class TestFeatureStub : IFullNodeFeature
        {
            /// <inheritdoc />
            public void Start()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public void Stop()
            {
                throw new NotImplementedException();
            }

            public void ValidateDependencies(IFullNodeServiceProvider services)
            {
                throw new NotImplementedException();
            }
        }

        private class TestFeatureStub2 : IFullNodeFeature
        {
            /// <inheritdoc />
            public void Start()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
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
