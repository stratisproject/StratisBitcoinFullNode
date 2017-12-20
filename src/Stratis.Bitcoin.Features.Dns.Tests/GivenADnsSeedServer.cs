using System;
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
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Tests for the <see cref="DnsSeedServer"/> class.
    /// </summary>
    public class GivenADnsSeedServer
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
            Action a = () => { new DnsSeedServer(null, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider); };

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
            Action a = () => { new DnsSeedServer(udpClient, null, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider); };

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
            Action a = () => { new DnsSeedServer(udpClient, masterFile, null, nodeLifetime, loggerFactory, dateTimeProvider); };

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
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, null, loggerFactory, dateTimeProvider); };

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
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, null, dateTimeProvider); };

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
            Action a = () => { new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, null); };

            // Act and Assert.
            a.ShouldThrow<ArgumentNullException>().Which.Message.Should().Contain("dateTimeProvider");
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

            // Act.
            DnsSeedServer server = new DnsSeedServer(udpClient, masterFile, asyncLoopFactory, nodeLifetime, loggerFactory, dateTimeProvider);

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

            // Act.
            CancellationTokenSource source = new CancellationTokenSource();
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider);
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

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;
            INodeLifetime nodeLifetime = new Mock<INodeLifetime>().Object;

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
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider);

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
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider);

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
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime, loggerFactory.Object, dateTimeProvider);

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
        public void WhenDnsServerInitialized_ThenMetricsLoopStarted()
        {
            // Arrange.
            Mock<IUdpClient> udpClient = new Mock<IUdpClient>();
            Mock<IMasterFile> masterFile = new Mock<IMasterFile>();

            CancellationTokenSource source = new CancellationTokenSource(5000);
            Mock<INodeLifetime> nodeLifetime = new Mock<INodeLifetime>();
            nodeLifetime.Setup(n => n.ApplicationStopping).Returns(source.Token);

            Mock<ILogger> logger = new Mock<ILogger>();
            bool startedLoop = false;
            logger.Setup(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>())).Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>((level, id, state, e, f) =>
            {
                startedLoop = state.ToString().Contains("DNS Metrics");
            });
            Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup<ILogger>(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            IAsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory(loggerFactory.Object);

            IDateTimeProvider dateTimeProvider = new Mock<IDateTimeProvider>().Object;

            // Act.
            DnsSeedServer server = new DnsSeedServer(udpClient.Object, masterFile.Object, asyncLoopFactory, nodeLifetime.Object, loggerFactory.Object, dateTimeProvider);
            server.Initialize();
            bool waited = source.Token.WaitHandle.WaitOne();

            // Assert.
            server.Should().NotBeNull();
            waited.Should().BeTrue();
            startedLoop.Should().BeTrue();
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
