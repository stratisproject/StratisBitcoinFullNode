using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class NetworkPeerTests
    {
        public NetworkPeerTests()
        {
        }

        [Fact]
        public void NetworkPeerRequirementCheckForOutboundWithValidVersionAndValidServiceReturnsTrue()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.True(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Network, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, false, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForOutboundWithValidVersionAndInvalidServiceReturnsFalse()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.False(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Nothing, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, false, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForOutboundWithInvalidVersionAndValidServiceReturnsFalse()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.False(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Network, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, false, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForOutboundWithInvalidVersionAndInvalidServiceReturnsFalse()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.False(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Nothing, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, false, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForInboundWithValidVersionAndValidServiceReturnsTrue()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.True(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Network, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, true, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForInboundWithValidVersionAndInvalidServiceReturnsTrue()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.True(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Nothing, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, true, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForInboundWithInvalidVersionAndValidServiceReturnsFalse()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.False(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Network, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, true, out string reason));
        }

        [Fact]
        public void NetworkPeerRequirementCheckForInboundWithInvalidVersionAndInvalidServiceReturnsFalse()
        {
            NetworkPeerRequirement networkPeerRequirement = new NetworkPeerRequirement();
            networkPeerRequirement.MinVersion = ProtocolVersion.PROTOCOL_VERSION;
            networkPeerRequirement.RequiredServices = NetworkPeerServices.Network;
            Assert.False(networkPeerRequirement.Check(new VersionPayload() { Services = NetworkPeerServices.Nothing, Version = ProtocolVersion.ALT_PROTOCOL_VERSION }, true, out string reason));
        }
    }
}