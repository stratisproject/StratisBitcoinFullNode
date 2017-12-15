using System;
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
        [Trait("UnitTest", "UnitTest")]
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
    }
}
