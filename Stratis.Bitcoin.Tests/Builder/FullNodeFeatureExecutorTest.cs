using Moq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
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

		public FullNodeFeatureExecutorTest()
		{			
			this.feature = new Mock<IFullNodeFeature>();
			this.feature2 = new Mock<IFullNodeFeature>();

			this.fullNodeServiceProvider = new Mock<IFullNodeServiceProvider>();
			this.fullNode = new Mock<IFullNode>();

			this.fullNode.Setup(f => f.Services)
				.Returns(this.fullNodeServiceProvider.Object);

			this.fullNodeServiceProvider.Setup(f => f.Features)
				.Returns(new List<IFullNodeFeature>() { this.feature.Object, this.feature2.Object });

            this.executor = new FullNodeFeatureExecutor(this.fullNode.Object, new LoggerFactory());
		}

		[Fact]
		public void StartCallsStartOnEachFeatureRegisterdWithFullNode()
		{
			this.executor.Start();

			this.feature.Verify(f => f.Start(), Times.Exactly(1));
			this.feature2.Verify(f => f.Start(), Times.Exactly(1));
		}

		[Fact]
		public void StartFeaturesThrowExceptionsCollectedInAggregateException()
		{
			Assert.Throws(typeof(AggregateException), () =>
			{
				this.feature.Setup(f => f.Start())
					.Throws(new ArgumentNullException());
				this.feature2.Setup(f => f.Start())
					.Throws(new ArgumentNullException());

				this.executor.Start();
			});
		}

		[Fact]
		public void StopCallsStopOnEachFeatureRegisterdWithFullNode()
		{
			this.executor.Stop();

			this.feature.Verify(f => f.Stop(), Times.Exactly(1));
			this.feature2.Verify(f => f.Stop(), Times.Exactly(1));
		}

		[Fact]
		public void StopFeaturesThrowExceptionsCollectedInAggregateException()
		{
			Assert.Throws(typeof(AggregateException), () =>
			{
				this.feature.Setup(f => f.Stop())
					.Throws(new ArgumentNullException());
				this.feature2.Setup(f => f.Stop())
					.Throws(new ArgumentNullException());

				this.executor.Stop();
			});
		}
	}
}
