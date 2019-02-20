using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Moq;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class NetworkPeerServerTests : LogsTestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;

        private readonly ITestOutputHelper testOutput;

        public NetworkPeerServerTests(ITestOutputHelper output)
        {
            this.testOutput = output;
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void Validate_AllowClientConnection_State(bool inIBD, bool isWhiteListed, bool closeClient)
        {
            // Arrange
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(),
                It.IsAny<NetworkPeerConnectionParameters>(),
                It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(new Mock<INetworkPeer>().Object));

            var initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();
            initialBlockDownloadState.Setup(i => i.IsInitialBlockDownload()).Returns(inIBD);

            var nodeSettings = new NodeSettings(KnownNetworks.RegTest);
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var endpointAddNode = new IPEndPoint(IPAddress.Parse("::ffff:192.168.0.1"), 80);

            var networkPeerServer = new NetworkPeerServer(this.Network,
                endpointAddNode, endpointAddNode, ProtocolVersion.PROTOCOL_VERSION, this.extendedLoggerFactory,
                networkPeerFactory.Object, initialBlockDownloadState.Object, connectionManagerSettings);

            // Mimic external client
            const int portNumber = 80;
            var client = new TcpClient("www.stratisplatform.com", portNumber);

            var ipandport = client.Client.RemoteEndPoint.ToString();
            var ip = ipandport.Replace(ipandport.Substring(ipandport.IndexOf(':')), "");

            var endpointDiscovered = new IPEndPoint(IPAddress.Parse(ip), portNumber);

            // Include the external client as a NodeServerEndpoint.
            connectionManagerSettings.Bind.Add(new NodeServerEndpoint(endpointDiscovered, isWhiteListed));

            // Act 
            var result = networkPeerServer.InvokeMethod("AllowClientConnection", client);

            // Assert
            Assert.True((inIBD && !isWhiteListed) == closeClient);

            this.testOutput.WriteLine(
                $"In IBD : {inIBD.ToString()}, " +
                $"Is White Listed : {isWhiteListed.ToString()}, " +
                $"Close Client : {result.ToString()}");
        }
    }
}