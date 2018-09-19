using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder
{
    public class FullNodeFeatureExecutorTest
    {
        private FullNodeFeatureExecutor executor;
        private Mock<IFullNodeFeature> feature;
        private Mock<IFullNodeFeature> feature2;
        private Mock<IFullNode> fullNode;
        private Mock<IFullNodeServiceProvider> fullNodeServiceProvider;

        /// <summary>
        /// Property that constructs a node feature executor.
        /// This is used in the missing dependency test.
        /// </summary>
        private FullNodeFeatureExecutor MissingFeatureExecutor
        {
            get
            {
                var feature = new Mock<IFullNodeFeature>();
                feature.Setup(f => f.ValidateDependencies(It.IsAny<IFullNodeServiceProvider>()))
                    .Throws(new MissingDependencyException());

                var fullNodeServiceProvider = new Mock<IFullNodeServiceProvider>();
                fullNodeServiceProvider.Setup(f => f.Features)
                    .Returns(new List<IFullNodeFeature> { feature.Object });
                var fullNode = new Mock<IFullNode>();
                fullNode.Setup(f => f.Services)
                    .Returns(fullNodeServiceProvider.Object);

                return new FullNodeFeatureExecutor(fullNode.Object, new LoggerFactory());
            }
        }

        public FullNodeFeatureExecutorTest()
        {
            this.feature = new Mock<IFullNodeFeature>();
            this.feature2 = new Mock<IFullNodeFeature>();

            this.fullNodeServiceProvider = new Mock<IFullNodeServiceProvider>();
            this.fullNode = new Mock<IFullNode>();

            this.fullNode.Setup(f => f.Services)
                .Returns(this.fullNodeServiceProvider.Object);

            this.fullNodeServiceProvider.Setup(f => f.Features)
                .Returns(new List<IFullNodeFeature> { this.feature.Object, this.feature2.Object });

            this.executor = new FullNodeFeatureExecutor(this.fullNode.Object, new LoggerFactory());
        }

        [Fact]
        public void StartCallsStartOnEachFeatureRegisterdWithFullNode()
        {
            this.executor.Initialize();

            this.feature.Verify(f => f.InitializeAsync(), Times.Exactly(1));
            this.feature2.Verify(f => f.InitializeAsync(), Times.Exactly(1));
        }

        [Fact]
        public void StartFeaturesThrowExceptionsCollectedInAggregateException()
        {
            Assert.Throws<AggregateException>(() =>
            {
                this.feature.Setup(f => f.InitializeAsync())
                    .Throws(new ArgumentNullException());
                this.feature2.Setup(f => f.InitializeAsync())
                    .Throws(new ArgumentNullException());

                this.executor.Initialize();
            });
        }

        [Fact]
        public void StopCallsStopOnEachFeatureRegisterdWithFullNode()
        {
            this.executor.Dispose();

            this.feature.Verify(f => f.Dispose(), Times.Exactly(1));
            this.feature2.Verify(f => f.Dispose(), Times.Exactly(1));
        }

        [Fact]
        public void StopFeaturesThrowExceptionsCollectedInAggregateException()
        {
            Assert.Throws<AggregateException>(() =>
            {
                this.feature.Setup(f => f.Dispose())
                    .Throws(new ArgumentNullException());
                this.feature2.Setup(f => f.Dispose())
                    .Throws(new ArgumentNullException());

                this.executor.Dispose();
            });
        }

        /// <summary>
        /// Test executor throws an exception from a missing feature dependency.
        /// </summary>
        [Fact]
        public void TestMissingDependencyThrowsException()
        {
            Assert.Throws<AggregateException>(() => this.MissingFeatureExecutor.Initialize());
        }
    }
}
