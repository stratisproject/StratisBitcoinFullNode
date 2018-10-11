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
using NSubstitute;
using NSubstitute.Extensions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Defines unit tests for the <see cref="WhitelistManager"/> class.
    /// </summary>
    public class GivenAWhitelistManager
    {
        private readonly IPeerBanning peerBanning;

        private Network Network;

        public GivenAWhitelistManager()
        {
            this.peerBanning = Substitute.For<IPeerBanning>();
            this.Network = KnownNetworks.TestNet;
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDatetimeProviderIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            Action a = () => { new WhitelistManager(null, null, null, null, null, null, this.peerBanning); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("dateTimeProvider");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndLoggerFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, null, null, null, null, null, this.peerBanning); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndPeerAddressManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, null, null, null, null, this.peerBanning); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("peerAddressManager");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDnsServerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, null, null, null, this.peerBanning); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("dnsServer");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndConnectionSettingsAreNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsHostName = "stratis.test.com";

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, dnsServer, null, dnsSettings, this.peerBanning); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("connectionSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndConnectionSettingsConnectionManagerIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsHostName = "stratis.test.com";

            Action a = () => { new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, dnsServer, null, dnsSettings, this.peerBanning); };

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("connectionSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndPeerBanningComponentIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IPeerAddressManager peerAddressManager = new Mock<IPeerAddressManager>().Object;
            IDnsServer dnsServer = new Mock<IDnsServer>().Object;
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsHostName = "stratis.test.com";
            ConnectionManagerSettings connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(this.Network));

            Action a = () => new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, dnsServer, connectionManagerSettings, dnsSettings, 
                    null);

            // Act and Assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("peerBanning");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndActivePeersAvailable_ThenWhitelistContainsActivePeers()
        {
            // Arrange.
            var mockDateTimeProvider = new Mock<IDateTimeProvider>();

            var mockLogger = new Mock<ILogger>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 2000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var activeEndpointOne = new IPEndPoint(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var activeEndpointTwo = new IPEndPoint(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var activeEndpointThree = new IPEndPoint(activeIpAddressThree, 80);

            IPAddress activeIpAddressFour = IPAddress.Parse("::ffff:192.168.0.4");
            var activeEndpointFour = new IPEndPoint(activeIpAddressFour, 80);

            IPAddress activeIpAddressFive = IPAddress.Parse("2607:f8b0:4009:80e::200e");
            var activeEndpointFive = new IPEndPoint(activeIpAddressFive, 80);

            var testDataSet = new List<Tuple<IPEndPoint, DateTimeOffset>>()
            {
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointFour, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointFive, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(50))
            };

            // PeerAddressManager does not support IPv4 addresses that are not represented as embedded IPv4 addresses in an IPv6 address.
            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(testDataSet);

            IMasterFile spiedMasterFile = null;
            var mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            NodeSettings nodeSettings = NodeSettings.Default(this.Network);
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";
            ConnectionManagerSettings connectionSettings = new ConnectionManagerSettings(nodeSettings);

            var whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, connectionSettings, dnsSettings, this.peerBanning);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            // Check for A records (IPv4 embedded in IPv6 and IPv4 addresses).
            var question4 = new Question(new Domain(dnsSettings.DnsHostName), RecordType.A);
            IList<IResourceRecord> resourceRecordsIpv4 = spiedMasterFile.Get(question4);
            resourceRecordsIpv4.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords4 = resourceRecordsIpv4.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords4.Should().HaveCount(4);

            // Check for AAAA records (IPv6 addresses).
            var question6 = new Question(new Domain(dnsSettings.DnsHostName), RecordType.AAAA);
            IList<IResourceRecord> resourceRecordsIpv6 = spiedMasterFile.Get(question6);
            resourceRecordsIpv6.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords6 = resourceRecordsIpv6.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords6.Should().HaveCount(1);

            foreach (Tuple<IPEndPoint, DateTimeOffset> testData in testDataSet)
            {
                if (testData.Item1.Address.IsIPv4MappedToIPv6)
                {
                    ipAddressResourceRecords4.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Address.MapToIPv4())).Should().NotBeNull();
                }
                else
                {
                    ipAddressResourceRecords6.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Address)).Should().NotBeNull();
                }
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndOwnIPInPeers_AndNotRunningFullNode_ThenWhitelistDoesNotContainOwnIP()
        {
            // Arrange.
            var mockDateTimeProvider = new Mock<IDateTimeProvider>();

            var mockLogger = new Mock<ILogger>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 5000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var activeEndpointOne = new IPEndPoint(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var activeEndpointTwo = new IPEndPoint(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var activeEndpointThree = new IPEndPoint(activeIpAddressThree, 80);

            var activeTestDataSet = new List<Tuple<IPEndPoint, DateTimeOffset>>()
            {
                new Tuple<IPEndPoint, DateTimeOffset> (activeEndpointOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
            };

            IPAddress externalIPAdress = IPAddress.Parse("::ffff:192.168.99.99");
            int externalPort = 80;
            var externalEndpoint = new IPEndPoint(externalIPAdress, externalPort);

            var externalIPTestDataSet = new List<Tuple<IPEndPoint, DateTimeOffset>>()
            {
                new Tuple<IPEndPoint, DateTimeOffset> (externalEndpoint,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40)),
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(activeTestDataSet.Union(externalIPTestDataSet).ToList());

            var args = new string[] {
                $"-dnspeeractivethreshold={inactiveTimePeriod.ToString()}",
                $"-externalip={externalEndpoint.Address.ToString()}",
                $"-port={externalPort.ToString()}",
            };

            IMasterFile spiedMasterFile = null;
            var mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            Network network = KnownNetworks.StratisTest;
            var nodeSettings = new NodeSettings(network, args: args);
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";
            dnsSettings.DnsFullNode = false;
            ConnectionManagerSettings connectionSettings = new ConnectionManagerSettings(nodeSettings);

            var whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, connectionSettings, dnsSettings, this.peerBanning);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            var question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.A);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(activeTestDataSet);

            foreach (Tuple<IPEndPoint, DateTimeOffset> testData in activeTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Address.MapToIPv4())).Should().NotBeNull();
            }

            // External IP.
            ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(externalEndpoint)).Should().BeNull("the external IP peer should not be in DNS as not running full node.");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndOwnIPInPeers_AndAreRunningFullNode_ThenWhitelistDoesContainOwnIP()
        {
            // Arrange.
            var mockDateTimeProvider = new Mock<IDateTimeProvider>();

            var mockLogger = new Mock<ILogger>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 5000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var activeEndpointOne = new IPEndPoint(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var activeEndpointTwo = new IPEndPoint(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var activeEndpointThree = new IPEndPoint(activeIpAddressThree, 80);

            IPAddress externalIPAdress = IPAddress.Parse("::ffff:192.168.99.99");
            int externalPort = 80;
            var externalEndpoint = new IPEndPoint(externalIPAdress, externalPort);

            var activeTestDataSet = new List<Tuple<IPEndPoint, DateTimeOffset>>()
            {
                new Tuple<IPEndPoint, DateTimeOffset> (activeEndpointOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
                new Tuple<IPEndPoint, DateTimeOffset> (externalEndpoint,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40))
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(activeTestDataSet);

            var args = new string[] {
                $"-dnspeeractivethreshold={inactiveTimePeriod.ToString()}",
                $"-externalip={externalEndpoint.Address.ToString()}",
                $"-port={externalPort.ToString()}",
            };

            IMasterFile spiedMasterFile = null;
            var mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            Network network = KnownNetworks.StratisTest;
            var nodeSettings = new NodeSettings(network, args: args);
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsFullNode = true;
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";
            ConnectionManagerSettings connectionSettings = new ConnectionManagerSettings(nodeSettings);

            var whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, connectionSettings, dnsSettings, this.peerBanning);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            var question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.A);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(activeTestDataSet);

            foreach (Tuple<IPEndPoint, DateTimeOffset> testData in activeTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Address.MapToIPv4())).Should().NotBeNull();
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndInactivePeersInWhitelist_ThenWhitelistDoesNotContainInactivePeers()
        {
            // Arrange.
            var mockDateTimeProvider = new Mock<IDateTimeProvider>();

            var mockLogger = new Mock<ILogger>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            mockDateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3))).Verifiable();
            IDateTimeProvider dateTimeProvider = mockDateTimeProvider.Object;

            int inactiveTimePeriod = 3000;

            IPAddress activeIpAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var activeEndpointOne = new IPEndPoint(activeIpAddressOne, 80);

            IPAddress activeIpAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var activeEndpointTwo = new IPEndPoint(activeIpAddressTwo, 80);

            IPAddress activeIpAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var activeEndpointThree = new IPEndPoint(activeIpAddressThree, 80);

            IPAddress activeIpAddressFour = IPAddress.Parse("::ffff:192.168.0.4");
            var activeEndpointFour = new IPEndPoint(activeIpAddressFour, 80);

            var activeTestDataSet = new List<Tuple<IPEndPoint, DateTimeOffset>>()
            {
                new Tuple<IPEndPoint, DateTimeOffset> (activeEndpointOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(10)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(20)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(30)),
                new Tuple<IPEndPoint, DateTimeOffset>(activeEndpointFour, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(40))
            };

            IPAddress inactiveIpAddressOne = IPAddress.Parse("::ffff:192.168.100.1");
            var inactiveEndpointOne = new IPEndPoint(inactiveIpAddressOne, 80);

            IPAddress inactiveIpAddressTwo = IPAddress.Parse("::ffff:192.168.100.2");
            var inactiveEndpointTwo = new IPEndPoint(inactiveIpAddressTwo, 80);

            IPAddress inactiveIpAddressThree = IPAddress.Parse("::ffff:192.168.100.3");
            var inactiveEndpointThree = new IPEndPoint(inactiveIpAddressThree, 80);

            var inactiveTestDataSet = new List<Tuple<IPEndPoint, DateTimeOffset>>()
            {
                new Tuple<IPEndPoint, DateTimeOffset> (inactiveEndpointOne,  dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(-10)),
                new Tuple<IPEndPoint, DateTimeOffset>(inactiveEndpointTwo, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(-20)),
                new Tuple<IPEndPoint, DateTimeOffset>(inactiveEndpointThree, dateTimeProvider.GetTimeOffset().AddSeconds(-inactiveTimePeriod).AddSeconds(-30))
            };

            IPeerAddressManager peerAddressManager = this.CreateTestPeerAddressManager(activeTestDataSet.Concat(inactiveTestDataSet).ToList());

            IMasterFile spiedMasterFile = null;
            var mockDnsServer = new Mock<IDnsServer>();
            mockDnsServer.Setup(d => d.SwapMasterfile(It.IsAny<IMasterFile>()))
                .Callback<IMasterFile>(m =>
                {
                    spiedMasterFile = m;
                })
                .Verifiable();

            NodeSettings nodeSettings = NodeSettings.Default(this.Network);
            DnsSettings dnsSettings = new DnsSettings(NodeSettings.Default(this.Network));
            dnsSettings.DnsPeerBlacklistThresholdInSeconds = inactiveTimePeriod;
            dnsSettings.DnsHostName = "stratis.test.com";
            ConnectionManagerSettings connectionSettings = new ConnectionManagerSettings(nodeSettings);

            var whitelistManager = new WhitelistManager(dateTimeProvider, loggerFactory, peerAddressManager, mockDnsServer.Object, connectionSettings, dnsSettings, this.peerBanning);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            spiedMasterFile.Should().NotBeNull();

            var question = new Question(new Domain(dnsSettings.DnsHostName), RecordType.A);
            IList<IResourceRecord> resourceRecords = spiedMasterFile.Get(question);
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveSameCount(activeTestDataSet);

            foreach (Tuple<IPEndPoint, DateTimeOffset> testData in activeTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Address.MapToIPv4())).Should().NotBeNull("the ip address is active and should be in DNS");
            }

            foreach (Tuple<IPEndPoint, DateTimeOffset> testData in inactiveTestDataSet)
            {
                ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testData.Item1.Address.MapToIPv4())).Should().BeNull("the ip address is inactive and should not be returned from DNS");
            }
        }

        [Theory]
        [InlineData("::ffff:192.168.0.2", "::ffff:192.168.0.3")]
        [InlineData("2607:f8b0:4009:80e::200e", "2607:f8b0:4009:80e::200f")]
        [Trait("DNS", "UnitTest")]
        public void WhenRefreshWhitelist_AndWhitelisted_IpV4_PeerIsBanned_ThenWhitelistDoesNotContainPeer(string bannedIp, string nonBannedIp)
        {
            // Arrange.
            var datetimeProvider = Substitute.For<IDateTimeProvider>();
            datetimeProvider.GetTimeOffset().Returns(new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3)));

            var logger = Substitute.For<ILogger>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

            var bannedEndpoint = new IPEndPoint(IPAddress.Parse(bannedIp), 80);
            var notBannedEndpoint = new IPEndPoint(IPAddress.Parse(nonBannedIp), 80);

            IPeerAddressManager peerAddressManager =
                this.CreateTestPeerAddressManager(new[] { bannedEndpoint, notBannedEndpoint });

            var dnsServer = Substitute.For<IDnsServer>();
            IMasterFile masterFile = null;
            dnsServer
                .When(m => m.SwapMasterfile(Arg.Any<IMasterFile>()))
                .Do(x => masterFile = x.Arg<IMasterFile>());

            var nodeSettings = NodeSettings.Default(this.Network);
            var dnsSettings = new DnsSettings(nodeSettings);
            dnsSettings.DnsHostName = "stratis.test.com";
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);

            peerBanning.IsBanned(bannedEndpoint)
                .Returns(true);

            var whitelistManager = new WhitelistManager(datetimeProvider, loggerFactory, peerAddressManager,
                dnsServer, connectionSettings, dnsSettings, peerBanning);

            // Act.
            whitelistManager.RefreshWhitelist();

            // Assert.
            masterFile.Should().NotBeNull();

            var ipAddressResourceRecords = masterFile
                .Get(new Question(new Domain(dnsSettings.DnsHostName), RecordType.ANY))
                .OfType<IPAddressResourceRecord>().ToList();

            ipAddressResourceRecords.Single().IPAddress.MapToIPv6()
                .Should().NotBe(bannedEndpoint.Address.MapToIPv6());
        }

        private IPeerAddressManager CreateTestPeerAddressManager(IEnumerable<IPEndPoint> testDataSet)
        {
            var dateTimeOffset = new DateTimeOffset(new DateTime(2017, 8, 30, 1, 2, 3));
            var timedEndpoints = testDataSet.Select(ip => new Tuple<IPEndPoint, DateTimeOffset>(ip, dateTimeOffset))
                .ToList();
            return CreateTestPeerAddressManager(timedEndpoints);
        }

        private IPeerAddressManager CreateTestPeerAddressManager(List<Tuple<IPEndPoint, DateTimeOffset>> testDataSet)
        {
            string dataFolderDirectory = Path.Combine(AppContext.BaseDirectory, "WhitelistTests");

            if (Directory.Exists(dataFolderDirectory))
            {
                Directory.Delete(dataFolderDirectory, true);
            }

            Directory.CreateDirectory(dataFolderDirectory);

            var peerFolder = new DataFolder(new NodeSettings(this.Network, args: new string[] { $"-datadir={dataFolderDirectory}" }).DataDir);

            var mockLogger = new Mock<ILogger>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            ILoggerFactory loggerFactory = mockLoggerFactory.Object;

            IPeerAddressManager peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, loggerFactory, new SelfEndpointTracker(loggerFactory));

            foreach (Tuple<IPEndPoint, DateTimeOffset> testData in testDataSet)
            {
                peerAddressManager.AddPeer(testData.Item1, IPAddress.Loopback);
                peerAddressManager.PeerSeen(testData.Item1, testData.Item2.DateTime);
            }

            return peerAddressManager;
        }
    }
}