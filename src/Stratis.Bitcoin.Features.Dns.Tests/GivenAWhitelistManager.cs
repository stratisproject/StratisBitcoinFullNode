using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Defines unit tests for the <see cref="WhitelistManager"/> class.
    /// </summary>
    public class GivenAWhitelistManager
    {
        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDatetimeProviderIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            Action a = () => { new WhitelistManager(null, null, null, null, null, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dateTimeProvider");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndLoggerFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, null, null, null, null, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndPeerAddressManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, null, null, null, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("peerAddressManager");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDnsServerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, null, null, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dnsServer");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeSettingsAreNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, dnsServer, null, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("nodeSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeSettingsDnsHostNameIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, dnsServer, nodeSettings, new DnsSettings(nodeSettings)); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("DnsHostName");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeSettingsConnectionManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            NodeSettings nodeSettings = NodeSettings.Default();
            DnsSettings dnsSettings = new DnsSettings(nodeSettings);
            dnsSettings.DnsHostName = "stratis.test.com";
            nodeSettings.ConnectionManager = null;

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, dnsServer, nodeSettings, dnsSettings); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("ConnectionManager");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndActivePeersAvailable_ThenWhitelistContainsActivePeers()
        {
            // Arrange.
            Mock<IDateTimeProvider> mockDateTimeProvider = new Mock<IDateTimeProvider>();

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 2000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            NetworkAddress activeNetworkAddressOne = new NetworkAddress(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            NetworkAddress activeNetworkAddressTwo = new NetworkAddress(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            NetworkAddress activeNetworkAddressThree = new NetworkAddress(activeIpAddressThree, 80);

            IPAddress activeIpAddressFour = IPAddress.Parse("::ffff:192.168.0.4");
            NetworkAddress activeNetworkAddressFour = new NetworkAddress(activeIpAddressFour, 80);

            List<Tuple<NetworkAddress, DateTimeOffset>> testDataSet = new List<Tuple<NetworkAddress, DateTimeOffset>>()
            {
                new Tuple<NetworkAddress, DateTimeOffset> (activeNetworkAddressOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressFour, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40))
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(testDataSet);

            IMasterFile spiedMasterFile = null;
            Mock<IDnsServer> mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            NodeSettings nodeSettings = NodeSettings.Default();
            DnsSettings dnsSettings = new DnsSettings(nodeSettings);
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";

            WhitelistManager whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, nodeSettings, dnsSettings);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            Question question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.AAAA);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(testDataSet);

            foreach (Tuple<NetworkAddress, DateTimeOffset> testData in testDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Endpoint.Address)).Should().NotBeNull();
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndOwnIPInPeers_AndNotRunningFullNode_ThenWhitelistDoesNotContainOwnIP()
        {
            // Arrange.
            Mock<IDateTimeProvider> mockDateTimeProvider = new Mock<IDateTimeProvider>();

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 5000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            NetworkAddress activeNetworkAddressOne = new NetworkAddress(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            NetworkAddress activeNetworkAddressTwo = new NetworkAddress(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            NetworkAddress activeNetworkAddressThree = new NetworkAddress(activeIpAddressThree, 80);

            List<Tuple<NetworkAddress, DateTimeOffset>> activeTestDataSet = new List<Tuple<NetworkAddress, DateTimeOffset>>()
            {
                new Tuple<NetworkAddress, DateTimeOffset> (activeNetworkAddressOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
            };

            IPAddress externalIPAdress = IPAddress.Parse("::ffff:192.168.99.99");
            int externalPort = 80;
            NetworkAddress externalNetworkAddress = new NetworkAddress(externalIPAdress, externalPort);

            List<Tuple<NetworkAddress, DateTimeOffset>> externalIPTestDataSet = new List<Tuple<NetworkAddress, DateTimeOffset>>()
            {
                new Tuple<NetworkAddress, DateTimeOffset> (externalNetworkAddress,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40)),
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(activeTestDataSet.Union(externalIPTestDataSet).ToList());

            string[] args = new string[] {
                $"-dnspeeractivethreshold={inactiveTimePeriod.ToString()}",
                $"-externalip={externalNetworkAddress.Endpoint.Address.ToString()}",
                $"-port={externalPort.ToString()}",
            };

            IMasterFile spiedMasterFile = null;
            Mock<IDnsServer> mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            Network network = Network.StratisTest;
            NodeSettings nodeSettings = new NodeSettings(network.Name, network).LoadArguments(args);
            DnsSettings dnsSettings = new DnsSettings(nodeSettings);
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";
            dnsSettings.DnsFullNode = false;

            WhitelistManager whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, nodeSettings, dnsSettings);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            Question question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.AAAA);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(activeTestDataSet);

            foreach (Tuple<NetworkAddress, DateTimeOffset> testData in activeTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Endpoint.Address)).Should().NotBeNull();
            }

            // External IP.
            ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(externalNetworkAddress.Endpoint)).Should().BeNull("the external IP peer should not be in DNS as not running full node.");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndOwnIPInPeers_AndAreRunningFullNode_ThenWhitelistDoesContainOwnIP()
        {
            // Arrange.
            Mock<IDateTimeProvider> mockDateTimeProvider = new Mock<IDateTimeProvider>();

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 5000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            NetworkAddress activeNetworkAddressOne = new NetworkAddress(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            NetworkAddress activeNetworkAddressTwo = new NetworkAddress(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            NetworkAddress activeNetworkAddressThree = new NetworkAddress(activeIpAddressThree, 80);

            IPAddress externalIPAdress = IPAddress.Parse("::ffff:192.168.99.99");
            int externalPort = 80;
            NetworkAddress externalNetworkAddress = new NetworkAddress(externalIPAdress, externalPort);

            List<Tuple<NetworkAddress, DateTimeOffset>> activeTestDataSet = new List<Tuple<NetworkAddress, DateTimeOffset>>()
            {
                new Tuple<NetworkAddress, DateTimeOffset> (activeNetworkAddressOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
                new Tuple<NetworkAddress, DateTimeOffset> (externalNetworkAddress,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40))
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(activeTestDataSet);

            string[] args = new string[] {
                $"-dnspeeractivethreshold={inactiveTimePeriod.ToString()}",
                $"-externalip={externalNetworkAddress.Endpoint.Address.ToString()}",
                $"-port={externalPort.ToString()}",
            };

            IMasterFile spiedMasterFile = null;
            Mock<IDnsServer> mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            Network network = Network.StratisTest;
            NodeSettings nodeSettings = new NodeSettings(network.Name, network).LoadArguments(args);
            DnsSettings dnsSettings = new DnsSettings(nodeSettings);
            dnsSettings.DnsFullNode = true;
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";

            WhitelistManager whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, nodeSettings, dnsSettings);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            Question question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.AAAA);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(activeTestDataSet);

            foreach (Tuple<NetworkAddress, DateTimeOffset> testData in activeTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Endpoint.Address)).Should().NotBeNull();
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndInactivePeersInWhitelist_ThenWhitelistDoesNotContainInactivePeers()
        {
            // Arrange.
            Mock<IDateTimeProvider> mockDateTimeProvider = new Mock<IDateTimeProvider>();

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 3000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            NetworkAddress activeNetworkAddressOne = new NetworkAddress(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            NetworkAddress activeNetworkAddressTwo = new NetworkAddress(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            NetworkAddress activeNetworkAddressThree = new NetworkAddress(activeIpAddressThree, 80);

            IPAddress activeIpAddressFour = IPAddress.Parse("::ffff:192.168.0.4");
            NetworkAddress activeNetworkAddressFour = new NetworkAddress(activeIpAddressFour, 80);

            List<Tuple<NetworkAddress, DateTimeOffset>> activeTestDataSet = new List<Tuple<NetworkAddress, DateTimeOffset>>()
            {
                new Tuple<NetworkAddress, DateTimeOffset> (activeNetworkAddressOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
                new Tuple<NetworkAddress, DateTimeOffset>(activeNetworkAddressFour, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40))
            };

            IPAddress inactiveIpAddressOne = IPAddress.Parse("::ffff:192.168.100.1");
            NetworkAddress inactiveNetworkAddressOne = new NetworkAddress(inactiveIpAddressOne, 80);

            IPAddress inactiveIpAddressTwo = IPAddress.Parse("::ffff:192.168.100.2");
            NetworkAddress inactiveNetworkAddressTwo = new NetworkAddress(inactiveIpAddressTwo, 80);

            IPAddress inactiveIpAddressThree = IPAddress.Parse("::ffff:192.168.100.3");
            NetworkAddress inactiveNetworkAddressThree = new NetworkAddress(inactiveIpAddressThree, 80);

            List<Tuple<NetworkAddress, DateTimeOffset>> inactiveTestDataSet = new List<Tuple<NetworkAddress, DateTimeOffset>>()
            {
                new Tuple<NetworkAddress, DateTimeOffset> (inactiveNetworkAddressOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(-10)),
                new Tuple<NetworkAddress, DateTimeOffset>(inactiveNetworkAddressTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(-20)),
                new Tuple<NetworkAddress, DateTimeOffset>(inactiveNetworkAddressThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(-30))
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(activeTestDataSet.Concat(inactiveTestDataSet).ToList());

            IMasterFile spiedMasterFile = null;
            Mock<IDnsServer> mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            NodeSettings nodeSettings = NodeSettings.Default();
            DnsSettings dnsSettings = new DnsSettings(nodeSettings);
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";

            WhitelistManager whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, nodeSettings, dnsSettings);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            Question question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.AAAA);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(activeTestDataSet);

            foreach (Tuple<NetworkAddress, DateTimeOffset> testData in activeTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Endpoint.Address)).Should().NotBeNull("the ip address is active and should be in DNS");
            }

            foreach (Tuple<NetworkAddress, DateTimeOffset> testData in inactiveTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Endpoint.Address)).Should().BeNull("the ip address is inactive and should not be returned from DNS");
            }
        }

        private IPeerAddressManager CreateTestPeerAddressManager(List<Tuple<NetworkAddress, DateTimeOffset>> testDataSet)
        {
            ConcurrentDictionary<IPEndPoint, PeerAddress> peers = new ConcurrentDictionary<IPEndPoint, PeerAddress>();

            string dataFolderDirectory = Path.Combine(AppContext.BaseDirectory, "WhitelistTests");

            if (Directory.Exists(dataFolderDirectory))
            {
                Directory.Delete(dataFolderDirectory, true);
            }
            Directory.CreateDirectory(dataFolderDirectory);

            var peerFolder = new DataFolder(new NodeSettings { DataDir = dataFolderDirectory });

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            Mock<ILoggerFactory> mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            IPeerAddressManager peerAddressManager = new PeerAddressManager(peerFolder, loggerFactory);

            foreach (Tuple<NetworkAddress, DateTimeOffset> testData in testDataSet)
            {
                peerAddressManager.AddPeer(testData.Item1, IPAddress.Loopback);
                peerAddressManager.PeerHandshaked(testData.Item1.Endpoint, testData.Item2);
            }

            return peerAddressManager;
        }
    }
}