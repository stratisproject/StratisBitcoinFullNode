﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Tests for the <see cref="DnsSeedServer"/> class.
    /// </summary>
    public class GivenADnsSeedServer : TestBase
    {
        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndUdpClientIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(null, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider, DnsSettings.Load(nodeSettings), dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("client");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndMasterFileIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(udpClient, null, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider, DnsSettings.Load(nodeSettings), dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("masterFile");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndAsyncLoopFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(udpClient, masterFile, null, nodeLifetime, loggerFactory, dateTimeProvider, DnsSettings.Load(nodeSettings), dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("asyncLoopFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndNodeLifetimeIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, null, loggerFactory, dateTimeProvider, DnsSettings.Load(nodeSettings), dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("nodeLifetime");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndLoggerFactoryIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, null, dateTimeProvider, DnsSettings.Load(nodeSettings), dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("loggerFactory");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDateTimeProviderIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, null, DnsSettings.Load(nodeSettings), dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dateTimeProvider");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDnsSettingsIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            DataFolder dataFolder = CreateDataFolder(this);
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider, null, dataFolder); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dnsSettings");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndDataFoldersIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            nodeSettings.DataDir = @"C:\";
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider, DnsSettings.Load(nodeSettings), null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dataFolders");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenConstructorCalled_AndAllParametersValid_ThenTypeCreated()
        {
            // Arrange.
            IUdpClient udpClient = new Mock<IUdpClient>().Object;
            IMasterFile masterFile = new Mock<IMasterFile>().Object;
            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            NodeSettings nodeSettings = NodeSettings.Default(args: new string[] { "-dnshostname=a", "-dnsnameserver=b", "-dnsmailbox=c" });
            DataFolder dataFolder = CreateDataFolder(this);

            // Act.
            DnsSeedServer server = new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider, DnsSettings.Load(nodeSettings), dataFolder);

            // Assert.
            server.Should().NotBeNull();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsServerListening_AndSocketExceptionRaised_ThenDnsServerFailsToStart()
        {
            // Arrange.
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            udpClient.Setup(c => c.StartListening(It.IsAny<int>())).Throws(new SocketException());

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            // Act.
            CancellationTokenSource source = new CancellationTokenSource();
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);
            Func<Task> func = async () => await server.ListenAsync(53, source.Token);

            // Assert.
            server.Should().NotBeNull();
            func.ShouldThrow<SocketException>();
            server.Metrics.DnsServerFailureCountSinceStart.Should().Be(1);
            server.Metrics.CurrentSnapshot.DnsServerFailureCountSinceLastPeriod.Should().Be(1);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public async Task WhenDnsServerReceiving_AndSocketExceptionRaised_ThenDnsServerFails_Async()
        {
            // Arrange.
            bool startedListening = false;
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            udpClient.Setup(c => c.StartListening(It.IsAny<int>())).Callback(() => startedListening = true);
            udpClient.Setup(c => c.ReceiveAsync()).ThrowsAsync(new SocketException());

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>() { new IPAddressResourceRecord(Domain.FromString("google.com"), IPAddress.Loopback) });

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>(MockBehavior.Loose);
            bool receivedSocketException = false;
            logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) =>
            {
                // Don't reset if we found the error message we were looking for
                if (!receivedSocketException)
                {
                    // Not yet set, check error message
                    receivedSocketException = state.ToString().StartsWith("Socket exception");
                }
            });
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            // Act.
            CancellationTokenSource source = new CancellationTokenSource(2000);
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);

            try
            {
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert.
            server.Should().NotBeNull();
            startedListening.Should().BeTrue();
            receivedSocketException.Should().BeTrue();
            server.Metrics.DnsRequestCountSinceStart.Should().BeGreaterThan(0);
            server.Metrics.DnsRequestFailureCountSinceStart.Should().BeGreaterThan(0);
            server.Metrics.DnsServerFailureCountSinceStart.Should().Be(0);
            server.Metrics.CurrentSnapshot.DnsRequestCountSinceLastPeriod.Should().BeGreaterThan(0);
            server.Metrics.CurrentSnapshot.DnsRequestFailureCountSinceLastPeriod.Should().BeGreaterThan(0);
            server.Metrics.CurrentSnapshot.DnsServerFailureCountSinceLastPeriod.Should().Be(0);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public async Task WhenDnsServerListening_AndDnsBadRequestReceived_ThenDnsServerFailsToProcessesRequest_Async()
        {
            // Arrange.
            bool startedListening = false;
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            udpClient.Setup(c => c.StartListening(It.IsAny<int>())).Callback(() => startedListening = true);
            udpClient.Setup(c => c.ReceiveAsync()).ReturnsAsync(new Tuple<IPEndPoint, byte[]>(new IPEndPoint(IPAddress.Loopback, 80), this.GetBadDnsRequest()));

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>() { new IPAddressResourceRecord(Domain.FromString("google.com"), IPAddress.Loopback) });

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>();
            bool receivedRequest = false;
            logger.Setup(l => l.Log(LogLevel.Trace, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) =>
            {
                // Don't reset if we found the trace message we were looking for
                if (!receivedRequest)
                {
                    // Not yet set, check trace message
                    receivedRequest = state.ToString().StartsWith("DNS request received");
                }
            });

            bool receivedBadRequest = false;
            logger.Setup(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) =>
            {
                // Don't reset if we found the warning message we were looking for
                if (!receivedBadRequest)
                {
                    // Not yet set, check warning message
                    receivedBadRequest = state.ToString().StartsWith("Failed to process DNS request");
                }
            });
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            // Act.
            CancellationTokenSource source = new CancellationTokenSource(2000);
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);

            try
            {
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert.
            server.Should().NotBeNull();
            startedListening.Should().BeTrue();
            receivedRequest.Should().BeTrue();
            receivedBadRequest.Should().BeTrue();
            server.Metrics.DnsRequestCountSinceStart.Should().BeGreaterThan(0);
            server.Metrics.DnsRequestFailureCountSinceStart.Should().BeGreaterThan(0);
            server.Metrics.DnsServerFailureCountSinceStart.Should().Be(0);
            server.Metrics.CurrentSnapshot.DnsRequestCountSinceLastPeriod.Should().BeGreaterThan(0);
            server.Metrics.CurrentSnapshot.DnsRequestFailureCountSinceLastPeriod.Should().BeGreaterThan(0);
            server.Metrics.CurrentSnapshot.DnsServerFailureCountSinceLastPeriod.Should().Be(0);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public async Task WhenDnsServerListening_AndDnsRequestReceived_ThenDnsServerSuccessfullyProcessesRequest_Async()
        {
            // Arrange.
            bool startedListening = false;
            bool sentResponse = false;
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            udpClient.Setup(c => c.StartListening(It.IsAny<int>())).Callback(() => startedListening = true);
            udpClient.Setup(c => c.ReceiveAsync()).ReturnsAsync(new Tuple<IPEndPoint, byte[]>(new IPEndPoint(IPAddress.Loopback, 80), this.GetDnsRequest()));
            udpClient.Setup(c => c.SendAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>())).Callback<byte[], int, IPEndPoint>((p, s, ip) => sentResponse = true).ReturnsAsync(1);

            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>() { new IPAddressResourceRecord(Domain.FromString("google.com"), IPAddress.Loopback) });

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>();
            bool receivedRequest = false;
            logger.Setup(l => l.Log(LogLevel.Trace, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) =>
            {
                // Don't reset if we found the trace message we were looking for
                if (!receivedRequest)
                {
                    // Not yet set, check trace message
                    receivedRequest = state.ToString().StartsWith("DNS request received");
                }
            });
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            // Act.
            CancellationTokenSource source = new CancellationTokenSource(2000);
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);

            try
            {
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert.
            server.Should().NotBeNull();
            startedListening.Should().BeTrue();
            receivedRequest.Should().BeTrue();
            sentResponse.Should().BeTrue();
            server.Metrics.DnsRequestCountSinceStart.Should().BeGreaterThan(0);
            server.Metrics.DnsRequestFailureCountSinceStart.Should().Be(0);
            server.Metrics.DnsServerFailureCountSinceStart.Should().Be(0);
            server.Metrics.CurrentSnapshot.DnsRequestCountSinceLastPeriod.Should().BeGreaterThan(0);
            server.Metrics.CurrentSnapshot.DnsRequestFailureCountSinceLastPeriod.Should().Be(0);
            server.Metrics.CurrentSnapshot.DnsServerFailureCountSinceLastPeriod.Should().Be(0);
            server.Metrics.CurrentSnapshot.DnsRequestElapsedTicksSinceLastPeriod.Should().BeGreaterThan(0);
            server.Metrics.CurrentSnapshot.LastDnsRequestElapsedTicks.Should().BeGreaterThan(0);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsServerInitialized_AndNoMasterFileOnDisk_ThenDnsServerSuccessfullyInitializes()
        {
            // Arrange.
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>());

            CancellationTokenSource source = new CancellationTokenSource(5000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            Mock<ILogger> logger = new Mock<ILogger>();
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            Mock<IAsyncLoopFactory> asyncLoopFactory = new Mock<IAsyncLoopFactory>();
            asyncLoopFactory.Setup(f => f.Run(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>())).Returns(new Mock<IAsyncLoop>().Object);
            
            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            string masterFilePath = Path.Combine(dataFolder.DnsMasterFilePath, DnsFeature.DnsMasterFileName);

            // Try and remove if already exists
            if (File.Exists(masterFilePath))
            {
                File.Delete(masterFilePath);
            }

            // Act.
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory.Object, nodeLifetime.Object, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);
            server.Initialize();
            bool waited = source.Token.WaitHandle.WaitOne();

            // Assert.
            server.Should().NotBeNull();
            waited.Should().BeTrue();
            masterFile.Verify(m => m.Load(It.IsAny<Stream>()), Times.Never);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsServerInitialized_AndMasterFileOnDisk_ThenDnsServerSuccessfullyInitializes()
        {
            // Arrange.
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>());

            CancellationTokenSource source = new CancellationTokenSource(5000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            Mock<ILogger> logger = new Mock<ILogger>();
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            Mock<IAsyncLoopFactory> asyncLoopFactory = new Mock<IAsyncLoopFactory>();
            asyncLoopFactory.Setup(f => f.Run(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>())).Returns(new Mock<IAsyncLoop>().Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            string masterFilePath = Path.Combine(dataFolder.DnsMasterFilePath, DnsFeature.DnsMasterFileName);

            // Act.
            try
            {
                // Create masterfile on disk
                using (FileStream stream = File.Create(masterFilePath))
                {
                    stream.Close();
                }

                // Run server
                DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory.Object, nodeLifetime.Object, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);
                server.Initialize();
                bool waited = source.Token.WaitHandle.WaitOne();

                // Assert.
                server.Should().NotBeNull();
                waited.Should().BeTrue();
                masterFile.Verify(m => m.Load(It.IsAny<Stream>()), Times.Once);
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
        public void WhenDnsServerInitialized_ThenMetricsLoopStarted()
        {
            // Arrange.
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>());

            CancellationTokenSource source = new CancellationTokenSource(5000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            Mock<ILogger> logger = new Mock<ILogger>();
            bool startedLoop = false;
            logger.Setup(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) =>
            {
                // Don't reset if we found the trace message we were looking for
                if (!startedLoop)
                {
                    // Not yet set, check trace message
                    startedLoop = state.ToString().Contains("DNS Metrics");
                }
            });
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            // Act.
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime.Object, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);
            server.Initialize();
            bool waited = source.Token.WaitHandle.WaitOne();

            // Assert.
            server.Should().NotBeNull();
            waited.Should().BeTrue();
            startedLoop.Should().BeTrue();
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenDnsServerInitialized_ThenSaveMasterfileLoopStarted()
        {
            // Arrange.
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();
            masterFile.Setup(m => m.Get(It.IsAny<Question>())).Returns(new List<IResourceRecord>());
            masterFile.Setup(m => m.Save(It.IsAny<Stream>())).Verifiable();

            CancellationTokenSource source = new CancellationTokenSource(5000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            Mock<ILogger> logger = new Mock<ILogger>();
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            // Act.
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime.Object, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);
            server.Initialize();
            bool waited = source.Token.WaitHandle.WaitOne();

            // Assert.
            server.Should().NotBeNull();
            waited.Should().BeTrue();
            masterFile.Verify(m => m.Save(It.IsAny<Stream>()), Times.AtLeastOnce);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public async Task WhenDnsServerListening_AndDnsRequestReceivedRepeatedly_ThenResponsesReturnedInRoundRobinOrder_Async()
        {
            // Arrange.
            Queue<CancellationTokenSource> sources = new Queue<CancellationTokenSource>();
            Queue<byte[]> responses = new Queue<byte[]>();
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            udpClient.Setup(c => c.ReceiveAsync()).ReturnsAsync(new Tuple<IPEndPoint, byte[]>(new IPEndPoint(IPAddress.Loopback, 80), this.GetDnsRequest()));
            udpClient.Setup(c => c.SendAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>())).Callback<byte[], int, IPEndPoint>((p, s, ip) =>
            {
                // One response at a time.
                responses.Enqueue(p);
                CancellationTokenSource source = sources.Dequeue();
                source.Cancel();
            }).ReturnsAsync(1);

            DnsSeedMasterFile masterFile = new DnsSeedMasterFile();
            masterFile.Add(new IPAddressResourceRecord(new Domain("google.com"), IPAddress.Parse("192.168.0.1")));
            masterFile.Add(new IPAddressResourceRecord(new Domain("google.com"), IPAddress.Parse("192.168.0.2")));
            masterFile.Add(new IPAddressResourceRecord(new Domain("google.com"), IPAddress.Parse("192.168.0.3")));
            masterFile.Add(new IPAddressResourceRecord(new Domain("google.com"), IPAddress.Parse("192.168.0.4")));

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;
            DnsSettings dnsSettings = new Mock<DnsSettings>().Object;
            dnsSettings.DnsHostName = "host.example.com";
            dnsSettings.DnsNameServer = "ns1.host.example.com";
            dnsSettings.DnsMailBox = "admin@host.example.com";
            DataFolder dataFolder = CreateDataFolder(this);

            Mock<ILogger> logger = new Mock<ILogger>();
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            // Act (Part 1).
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider, dnsSettings, dataFolder);

            try
            {
                CancellationTokenSource source = new CancellationTokenSource();
                sources.Enqueue(source);
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            // Assert (Part 1).
            responses.Count.Should().Be(1);
            byte[] response = responses.Dequeue();
            IResponse dnsResponse = Response.FromArray(response);

            dnsResponse.AnswerRecords.Count.Should().Be(4);
            dnsResponse.AnswerRecords[0].Should().BeOfType<IPAddressResourceRecord>();

            ((IPAddressResourceRecord)dnsResponse.AnswerRecords[0]).IPAddress.ToString().Should().Be("192.168.0.1");

            while (responses.Count > 0)
            {
                // Consume queue completely.
                responses.Dequeue();
            }

            // Act (Part 2).
            try
            {
                CancellationTokenSource source = new CancellationTokenSource();
                sources.Enqueue(source);
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            // Assert (Part 2).
            responses.Count.Should().Be(1);
            response = responses.Dequeue();
            dnsResponse = Response.FromArray(response);

            dnsResponse.AnswerRecords.Count.Should().Be(4);
            dnsResponse.AnswerRecords[0].Should().BeOfType<IPAddressResourceRecord>();

            ((IPAddressResourceRecord)dnsResponse.AnswerRecords[0]).IPAddress.ToString().Should().Be("192.168.0.2");

            while (responses.Count > 0)
            {
                // Consume queue completely.
                responses.Dequeue();
            }

            // Act (Part 3).
            try
            {
                CancellationTokenSource source = new CancellationTokenSource();
                sources.Enqueue(source);
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            // Assert (Part 3).
            responses.Count.Should().Be(1);
            response = responses.Dequeue();
            dnsResponse = Response.FromArray(response);

            dnsResponse.AnswerRecords.Count.Should().Be(4);
            dnsResponse.AnswerRecords[0].Should().BeOfType<IPAddressResourceRecord>();

            ((IPAddressResourceRecord)dnsResponse.AnswerRecords[0]).IPAddress.ToString().Should().Be("192.168.0.3");

            while (responses.Count > 0)
            {
                // Consume queue completely.
                responses.Dequeue();
            }

            // Act (Part 4).
            try
            {
                CancellationTokenSource source = new CancellationTokenSource();
                sources.Enqueue(source);
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            // Assert (Part 4).
            responses.Count.Should().Be(1);
            response = responses.Dequeue();
            dnsResponse = Response.FromArray(response);

            dnsResponse.AnswerRecords.Count.Should().Be(4);
            dnsResponse.AnswerRecords[0].Should().BeOfType<IPAddressResourceRecord>();

            ((IPAddressResourceRecord)dnsResponse.AnswerRecords[0]).IPAddress.ToString().Should().Be("192.168.0.4");

            while (responses.Count > 0)
            {
                // Consume queue completely.
                responses.Dequeue();
            }

            // Act (Part 5).
            try
            {
                CancellationTokenSource source = new CancellationTokenSource();
                sources.Enqueue(source);
                await server.ListenAsync(53, source.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            // Assert (Part 5).
            responses.Count.Should().Be(1);
            response = responses.Dequeue();
            dnsResponse = Response.FromArray(response);

            dnsResponse.AnswerRecords.Count.Should().Be(4);
            dnsResponse.AnswerRecords[0].Should().BeOfType<IPAddressResourceRecord>();

            // This should start back at the beginning again.
            ((IPAddressResourceRecord)dnsResponse.AnswerRecords[0]).IPAddress.ToString().Should().Be("192.168.0.1");

            while (responses.Count > 0)
            {
                // Consume queue completely.
                responses.Dequeue();
            }
        }

        /// <summary>
        /// Sets up a DNS 'A' request for google.com.
        /// </summary>
        /// <returns>Returns a test DNS request buffer.</returns>
        private byte[] GetDnsRequest()
        {
            return new byte[] { 236, 17, 1, 32, 0, 1, 0, 0, 0, 0, 0, 1, 6, 103, 111, 111, 103, 108, 101, 3, 99, 111, 109, 0, 0, 1, 0, 1, 0, 0, 41, 16, 0, 0, 0, 0, 0, 0, 0 };
        }

        /// <summary>
        /// Sets up a bad DNS 'A' request.
        /// </summary>
        /// <returns>Returns a test DNS request buffer.</returns>
        private byte[] GetBadDnsRequest()
        {
            return new byte[] { 255, 2, 255, 3, 0, 1, 0, 0, 0, 0, 0, 1, 6, 103, 111, 111, 103, 108, 101, 3, 99, 111, 109, 0, 0, 1, 0, 1, 0, 0, 41, 16, 0, 0, 0, 0, 0, 0, 0 };
        }
    }
}
