using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class LeaderReceiverTests : IDisposable
    {
        private ILeaderReceiver leaderReceiver;
        private IDisposable streamSubscription;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;
        private readonly ILeaderProvider leaderProvider;

        private const string PublicKey = "026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c";

        public LeaderReceiverTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.leaderProvider = Substitute.For<ILeaderProvider>();
        }

        [Fact]
        public void ReceiveLeaders_Should_Push_Items_Into_The_LeaderProvidersStream()
        {
            this.leaderReceiver = new LeaderReceiver(this.loggerFactory);

            const int LeaderCount = 3;
            var receivedLeaderCount = 0;

            this.streamSubscription = this.leaderReceiver.LeaderProvidersStream.Subscribe(
                _ => { receivedLeaderCount++; });

            this.leaderProvider.CurrentLeader.Returns(new NBitcoin.PubKey(PublicKey));

            for (var i = 0; i < LeaderCount; i++)
                this.leaderReceiver.ReceiveLeader(this.leaderProvider);

            receivedLeaderCount.Should().Be(LeaderCount);

            var logMsg = string.Format("Received federated leader: {0}", PublicKey);

            this.logger.Received(receivedLeaderCount).Log(LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString() == logMsg),
                null,
                Arg.Any<Func<object, Exception, string>>());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.streamSubscription?.Dispose();
            this.leaderReceiver?.Dispose();
        }
    }
}
