using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Tests for the <see cref="DnsFeature"/> class.
    /// </summary>
    public class GivenADnsFeature : TestBase
    {
        private class TestContext
        {
            public Mock<IDnsServer> dnsServer;
            public Mock<IWhitelistManager> whitelistManager;
            public Mock<ILoggerFactory> loggerFactory;
            public Mock<INodeLifetime> nodeLifetime;
            public DnsSettings dnsSettings;
            public NodeSettings nodeSettings;
            public DataFolder dataFolder;
            public IAsyncLoopFactory asyncLoopFactory;
            public Mock<IConnectionManager> connectionManager;
            public UnreliablePeerBehavior unreliablePeerBehavior;

            public TestContext(Network network)
            {
                this.dnsServer = new Mock<IDnsServer>();
                this.whitelistManager = new Mock<IWhitelistManager>();

                var logger = new Mock<ILogger>(MockBehavior.Loose);
                this.loggerFactory = new Mock<ILoggerFactory>();
                this.loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

                this.nodeLifetime = new Mock<INodeLifetime>();
                this.nodeSettings = new NodeSettings(network, args: new string[] { $"-datadir={Directory.GetCurrentDirectory()}" });
                this.dnsSettings = new DnsSettings(this.nodeSettings);
                this.dataFolder = CreateDataFolder(this);
                this.asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
                this.connectionManager = this.BuildConnectionManager();
                this.unreliablePeerBehavior = this.BuildUnreliablePeerBehavior();
            }

            private Mock<IConnectionManager> BuildConnectionManager()
            {
                NetworkPeerConnectionParameters networkPeerParameters = new NetworkPeerConnectionParameters();
                Mock<IConnectionManager> connectionManager = new Mock<IConnectionManager>();
                connectionManager.SetupGet(np => np.Parameters).Returns(networkPeerParameters);
                connectionManager.SetupGet(np => np.ConnectedPeers).Returns(new NetworkPeerCollection());
                return connectionManager;
            }

            private UnreliablePeerBehavior BuildUnreliablePeerBehavior()
            {
                IChainState chainState = new Mock<IChainState>().Object;
                IPeerBanning peerBanning = new Mock<IPeerBanning>().Object;
                Checkpoints checkpoints = new Checkpoints();
                return new UnreliablePeerBehavior(KnownNetworks.StratisMain, chainState, this.loggerFactory.Object, peerBanning, this.nodeSettings, checkpoints);
            }
        }

        private readonly TestContext defaultConstructorParameters;

        public GivenADnsFeature() : base(KnownNetworks.Main)
        {
            this.defaultConstructorParameters = new TestContext(this.Network);
        }

        /// <summary>
        /// Builds the default DNS feature using default constructor parameters.
        /// </summary>
        /// <returns>DnsFeature instance.</returns>
        private DnsFeature BuildDefaultDnsFeature()
        {
            return new DnsFeature(
                this.defaultConstructorParameters.dnsServer?.Object,
                this.defaultConstructorParameters.whitelistManager?.Object,
                this.defaultConstructorParameters.loggerFactory?.Object,
                this.defaultConstructorParameters.nodeLifetime?.Object,
                this.defaultConstructorParameters.dnsSettings,
                this.defaultConstructorParameters.nodeSettings,
                this.defaultConstructorParameters.dataFolder,
                this.defaultConstructorParameters.asyncLoopFactory,
                this.defaultConstructorParameters.connectionManager?.Object,
                this.defaultConstructorParameters.unreliablePeerBehavior
            );
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDnsServerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            this.defaultConstructorParameters.dnsServer = null;
            Action a = () => this.BuildDefaultDnsFeature();

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("dnsServer");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndWhiteListManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            this.defaultConstructorParameters.whitelistManager = null;
            Action a = () => this.BuildDefaultDnsFeature();

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("whitelistManager");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndLoggerFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            this.defaultConstructorParameters.loggerFactory = null;
            Action a = () => this.BuildDefaultDnsFeature();

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeLifetimeIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            this.defaultConstructorParameters.nodeLifetime = null;
            Action a = () => this.BuildDefaultDnsFeature();

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("nodeLifetime");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeSettingsIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            this.defaultConstructorParameters.nodeSettings = null;
            Action a = () => this.BuildDefaultDnsFeature();

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("nodeSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDataFolderIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            this.defaultConstructorParameters.dataFolder = null;
            Action a = () => this.BuildDefaultDnsFeature();

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("dataFolder");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndAllParametersValid_ThenTypeCreated()
        {
            // Arrange.
            var feature = this.BuildDefaultDnsFeature();

            // Assert.
            feature.Should().NotBeNull();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsFeatureInitialized_ThenDnsServerSuccessfullyStarts()
        {
            // Arrange.
            var waitObject = new ManualResetEventSlim(false);
            Action<int, CancellationToken> action = (port, token) =>
            {
                waitObject.Set();
                throw new OperationCanceledException();
            };
            this.defaultConstructorParameters.dnsServer.Setup(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(action);

            // Act.
            var feature = this.BuildDefaultDnsFeature();
            feature.InitializeAsync().GetAwaiter().GetResult();
            bool waited = waitObject.Wait(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            this.defaultConstructorParameters.dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsFeatureStopped_ThenDnsServerSuccessfullyStops()
        {
            // Arrange.
            Action<int, CancellationToken> action = (port, token) =>
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(50);
                }
            };
            this.defaultConstructorParameters.dnsServer.Setup(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(action);

            var source = new CancellationTokenSource();
            this.defaultConstructorParameters.nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            this.defaultConstructorParameters.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            // Act.
            var feature = this.BuildDefaultDnsFeature();
            feature.InitializeAsync().GetAwaiter().GetResult();
            this.defaultConstructorParameters.nodeLifetime.Object.StopApplication();
            bool waited = source.Token.WaitHandle.WaitOne(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            this.defaultConstructorParameters.dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsServerFailsToStart_ThenDnsFeatureRetries()
        {
            // Arrange.
            Action<int, CancellationToken> action = (port, token) =>
            {
                throw new ArgumentException("Bad port");
            };
            this.defaultConstructorParameters.dnsServer.Setup(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(action);

            var source = new CancellationTokenSource(3000);
            this.defaultConstructorParameters.nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            this.defaultConstructorParameters.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            var logger = new Mock<ILogger>();
            bool serverError = false;
            logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) => serverError = state.ToString().StartsWith("Failed whilst running the DNS server"));
            this.defaultConstructorParameters.loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            // Act.
            var feature = this.BuildDefaultDnsFeature();
            feature.InitializeAsync().GetAwaiter().GetResult();
            bool waited = source.Token.WaitHandle.WaitOne(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            this.defaultConstructorParameters.dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            serverError.Should().BeTrue();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenInitialize_ThenRefreshLoopIsStarted()
        {
            // Arrange.
            this.defaultConstructorParameters.whitelistManager.Setup(w => w.RefreshWhitelist()).Verifiable("the RefreshWhitelist method should be called on the WhitelistManager");

            var source = new CancellationTokenSource(3000);
            this.defaultConstructorParameters.nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            this.defaultConstructorParameters.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            this.defaultConstructorParameters.asyncLoopFactory = new AsyncLoopFactory(this.defaultConstructorParameters.loggerFactory.Object);

            using (var feature = this.BuildDefaultDnsFeature())
            {
                // Act.
                feature.InitializeAsync().GetAwaiter().GetResult();
                bool waited = source.Token.WaitHandle.WaitOne(5000);

                // Assert.
                feature.Should().NotBeNull();
                waited.Should().BeTrue();
                this.defaultConstructorParameters.whitelistManager.Verify();
            }
        }
    }
}