using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
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
        public GivenADnsFeature() : base(Network.Main)
        {
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDnsServerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IWhitelistManager whitelistManager = new Mock<IWhitelistManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            DataFolder dataFolder = CreateDataFolder(this);
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            Action a = () => { new DnsFeature(null, whitelistManager, loggerFactory, nodeLifetime, dnsSettings, nodeSettings, dataFolder, asyncLoopFactory); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("dnsServer");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndWhiteListManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DataFolder dataFolder = CreateDataFolder(this);
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            Action a = () => { new DnsFeature(dnsServer, null, loggerFactory, nodeLifetime, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("whitelistManager");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndLoggerFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IWhitelistManager whitelistManager = new Mock<IWhitelistManager>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DataFolder dataFolder = CreateDataFolder(this);
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            Action a = () => { new DnsFeature(dnsServer, whitelistManager, null, nodeLifetime, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeLifetimeIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IWhitelistManager whitelistManager = new Mock<IWhitelistManager>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DataFolder dataFolder = CreateDataFolder(this);
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            Action a = () => { new DnsFeature(dnsServer, whitelistManager, loggerFactory, null, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("nodeLifetime");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeSettingsIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IWhitelistManager whitelistManager = new Mock<IWhitelistManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DataFolder dataFolder = CreateDataFolder(this);
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            Action a = () => { new DnsFeature(dnsServer, whitelistManager, loggerFactory, nodeLifetime, new DnsSettings().Load(nodeSettings), null, dataFolder, asyncLoopFactory); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("nodeSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AnddataFolderIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IWhitelistManager whitelistManager = new Mock<IWhitelistManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = new Mock<NodeSettings>(null, NodeSettings.SupportedProtocolVersion, "StratisBitcoin", null, null).Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            Action a = () => { new DnsFeature(dnsServer, whitelistManager, loggerFactory, nodeLifetime, dnsSettings, nodeSettings, null, asyncLoopFactory); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("dataFolder");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndAllParametersValid_ThenTypeCreated()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IWhitelistManager whitelistManager = new Mock<IWhitelistManager>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DataFolder dataFolder = CreateDataFolder(this);
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer, whitelistManager, loggerFactory, nodeLifetime, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory);

            // Assert.
            feature.Should().NotBeNull();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsFeatureInitialized_ThenDnsServerSuccessfullyStarts()
        {
            // Arrange.
            Mock<IDnsServer> dnsServer = new Mock<IDnsServer>();
            ManualResetEventSlim waitObject = new ManualResetEventSlim(false);
            Action<int, CancellationToken> action = (port, token) =>
            {
                waitObject.Set();
                throw new OperationCanceledException();
            };
            dnsServer.Setup(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(action);

            Mock<IWhitelistManager> whitelistManager = new Mock<IWhitelistManager>();

            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            NodeSettings nodeSettings = new NodeSettings(args:new string[] { $"-datadir={Directory.GetCurrentDirectory()}" });
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory.Object);

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer.Object, whitelistManager.Object, loggerFactory.Object, nodeLifetime.Object, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory);
            feature.Initialize();
            bool waited = waitObject.Wait(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsFeatureStopped_ThenDnsServerSuccessfullyStops()
        {
            // Arrange.
            Mock<IDnsServer> dnsServer = new Mock<IDnsServer>();
            Action<int, CancellationToken> action = (port, token) =>
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(50);
                }
            };
            dnsServer.Setup(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(action);

            Mock<IWhitelistManager> mockWhitelistManager = new Mock<IWhitelistManager>();
            IWhitelistManager whitelistManager = mockWhitelistManager.Object;

            CancellationTokenSource source = new CancellationTokenSource();
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);
            INodeLifetime nodeLifetimeObject = nodeLifetime.Object;

            NodeSettings nodeSettings = new NodeSettings(args:new string[] { $"-datadir={ Directory.GetCurrentDirectory() }" });
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory.Object);

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer.Object, whitelistManager, loggerFactory.Object, nodeLifetimeObject, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory);
            feature.Initialize();
            nodeLifetimeObject.StopApplication();
            bool waited = source.Token.WaitHandle.WaitOne(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsServerFailsToStart_ThenDnsFeatureRetries()
        {
            // Arrange.
            Mock<IDnsServer> dnsServer = new Mock<IDnsServer>();
            Action<int, CancellationToken> action = (port, token) =>
            {
                throw new ArgumentException("Bad port");
            };
            dnsServer.Setup(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(action);

            Mock<IWhitelistManager> mockWhitelistManager = new Mock<IWhitelistManager>();
            IWhitelistManager whitelistManager = mockWhitelistManager.Object;

            CancellationTokenSource source = new CancellationTokenSource(3000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);
            INodeLifetime nodeLifetimeObject = nodeLifetime.Object;

            NodeSettings nodeSettings = new NodeSettings(args: new string[] { $"-datadir={ Directory.GetCurrentDirectory() }" });
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>();
            bool serverError = false;
            logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) => serverError = state.ToString().StartsWith("Failed whilst running the DNS server"));
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory.Object);

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer.Object, whitelistManager, loggerFactory.Object, nodeLifetimeObject, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory);
            feature.Initialize();
            bool waited = source.Token.WaitHandle.WaitOne(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            serverError.Should().BeTrue();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenInitialize_ThenRefreshLoopIsStarted()
        {
            // Arrange.
            Mock<IWhitelistManager> mockWhitelistManager = new Mock<IWhitelistManager>();
            mockWhitelistManager.Setup(w => w.RefreshWhitelist()).Verifiable("the RefreshWhitelist method should be called on the WhitelistManager");

            IWhitelistManager whitelistManager = mockWhitelistManager.Object;

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            INodeLifetime nodeLifeTime = new NodeLifetime();

            IDnsServer dnsServer = new Mock<IDnsServer>().Object;

            CancellationTokenSource source = new CancellationTokenSource(3000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);
            INodeLifetime nodeLifetimeObject = nodeLifetime.Object;

            NodeSettings nodeSettings = new NodeSettings(args: new string[] { $"-datadir={ Directory.GetCurrentDirectory() }" });
            DataFolder dataFolder = CreateDataFolder(this);

            using (DnsFeature feature = new DnsFeature(dnsServer, whitelistManager, loggerFactory, nodeLifetimeObject, new DnsSettings().Load(nodeSettings), nodeSettings, dataFolder, asyncLoopFactory))
            {
                // Act.
                feature.Initialize();
                bool waited = source.Token.WaitHandle.WaitOne(5000);

                // Assert.
                feature.Should().NotBeNull();
                waited.Should().BeTrue();
                mockWhitelistManager.Verify();
            }
        }
    }
}