using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Tests for the <see cref="DnsFeature"/> class.
    /// </summary>
    public class GivenADnsFeature
    {
        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDnsServerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            Action a = () => { new DnsFeature(null, masterFile, peerAddressManager, loggerFactory, nodeLifetime, nodeSettings, dataFolders); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dnsServer");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndMasterFileIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            Action a = () => { new DnsFeature(dnsServer, null, peerAddressManager, loggerFactory, nodeLifetime, nodeSettings, dataFolders); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("masterFile");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndPeerAddressManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            Action a = () => { new DnsFeature(dnsServer, masterFile, null, loggerFactory, nodeLifetime, nodeSettings, dataFolders); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("peerAddressManager");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndLoggerFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            Action a = () => { new DnsFeature(dnsServer, masterFile, peerAddressManager, null, nodeLifetime, nodeSettings, dataFolders); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeLifetimeIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            Action a = () => { new DnsFeature(dnsServer, masterFile, peerAddressManager, loggerFactory, null, nodeSettings, dataFolders); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("nodeLifetime");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeSettingsIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            Action a = () => { new DnsFeature(dnsServer, masterFile, peerAddressManager, loggerFactory, nodeLifetime, null, dataFolders); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("nodeSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDataFoldersIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = new Mock<NodeSettings>("bitcoin", null, NodeSettings.SupportedProtocolVersion, "StratisBitcoin").Object;
            Action a = () => { new DnsFeature(dnsServer, masterFile, peerAddressManager, loggerFactory, nodeLifetime, nodeSettings, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dataFolders");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndAllParametersValid_ThenTypeCreated()
        {
            // Arrange.
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = @"C:\";
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer, masterFile, peerAddressManager, loggerFactory, nodeLifetime, nodeSettings, dataFolders);

            // Assert.
            feature.Should().NotBeNull();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsFeatureInitialized_AndDnsServerStarted_AndNoMasterFileOnDisk_ThenDnsServerSuccessfullyStarts()
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
            dnsServer.Setup(s => s.SwapMasterfile(It.IsAny<IMasterFile>())).Verifiable();

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Load(It.IsAny<Stream>())).Verifiable();

            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;

            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = Directory.GetCurrentDirectory();
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer.Object, masterFile.Object, peerAddressManager, loggerFactory.Object, nodeLifetime.Object, nodeSettings, dataFolders);
            feature.Initialize();
            bool waited = waitObject.Wait(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            masterFile.Verify(m => m.Load(It.IsAny<Stream>()), Times.Never);
            dnsServer.Verify(s => s.SwapMasterfile(It.IsAny<IMasterFile>()), Times.Never);
            dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsFeatureInitialized_AndDnsServerStarted_AndMasterFileOnDisk_ThenDnsServerSuccessfullyStarts()
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
            dnsServer.Setup(s => s.SwapMasterfile(It.IsAny<IMasterFile>())).Verifiable();

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Load(It.IsAny<Stream>())).Verifiable();

            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;

            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = Directory.GetCurrentDirectory();
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            string masterFilePath = Path.Combine(dataFolders.DnsMasterFilePath, DnsFeature.DnsMasterFileName);

            // Act.
            try
            {
                // Create masterfile on disk
                using (FileStream stream = File.Create(masterFilePath))
                {
                    stream.Close();
                }

                // Run feature
                DnsFeature feature = new DnsFeature(dnsServer.Object, masterFile.Object, peerAddressManager, loggerFactory.Object, nodeLifetime.Object, nodeSettings, dataFolders);
                feature.Initialize();
                bool waited = waitObject.Wait(5000);

                // Assert.
                feature.Should().NotBeNull();
                waited.Should().BeTrue();
                masterFile.Verify(m => m.Load(It.IsAny<Stream>()), Times.Once);
                dnsServer.Verify(s => s.SwapMasterfile(It.IsAny<IMasterFile>()), Times.Once);
                dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                // Try and remove created
                if (File.Exists(masterFilePath))
                {
                    File.Delete(masterFilePath);
                }
            }
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
            dnsServer.Setup(s => s.SwapMasterfile(It.IsAny<IMasterFile>())).Verifiable();

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Load(It.IsAny<Stream>())).Verifiable();

            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;

            CancellationTokenSource source = new CancellationTokenSource();
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.StopApplication()).Callback(() => source.Cancel());
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);
            INodeLifetime nodeLifetimeObject = nodeLifetime.Object;

            NodeSettings nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = Directory.GetCurrentDirectory();
            DataFolder dataFolders = new Mock<DataFolder>(nodeSettings).Object;

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            // Act.
            DnsFeature feature = new DnsFeature(dnsServer.Object, masterFile.Object, peerAddressManager, loggerFactory.Object, nodeLifetimeObject, nodeSettings, dataFolders);
            feature.Initialize();
            nodeLifetimeObject.StopApplication();
            bool waited = source.Token.WaitHandle.WaitOne(5000);

            // Assert.
            feature.Should().NotBeNull();
            waited.Should().BeTrue();
            masterFile.Verify(m => m.Load(It.IsAny<Stream>()), Times.Never);
            dnsServer.Verify(s => s.SwapMasterfile(It.IsAny<IMasterFile>()), Times.Never);
            dnsServer.Verify(s => s.ListenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
