using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling
{
    public class EnforcePeerVersionCheckBehaviorTests : LogsTestBase
    {
        private class EnforcePeerVersionCheckBehaviorWrapper : EnforcePeerVersionCheckBehavior
        {
            public EnforcePeerVersionCheckBehaviorWrapper(ChainIndexer chainIndexer, NodeSettings nodeSettings, Network network, ILoggerFactory loggerFactory) : base(chainIndexer, nodeSettings, network, loggerFactory)
            {
            }

            public Task TestOnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
            {
                return OnMessageReceivedAsync(peer, message);
            }

            protected override void AttachCore()
            {
            }
        }

        private INetworkPeer CreateNetworkPeer(ProtocolVersion version)
        {
            var peerVersion = new VersionPayload
            {
                Version = version
            };

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerVersion).Returns(peerVersion);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.Connected);
            networkPeer.Setup(m => m.Disconnect(It.IsAny<string>(), It.IsAny<Exception>())).Callback((string message, Exception exception) => Disconnected(networkPeer, message, exception));

            return networkPeer.Object;
        }
        
        private void Disconnected(Mock<INetworkPeer> peer, string reason, Exception exception)
        {
            peer.SetupGet(n => n.State).Returns(NetworkPeerState.Offline);
        }

        [Fact]
        public void IncompatibileNodesDisconnectAfterHardFork()
        {
            // Set the hard-fork parameters.
            this.Network.Consensus.Options.EnforceMinProtocolVersionAtBlockHeight = 5;
            this.Network.Consensus.Options.EnforcedMinProtocolVersion = ProtocolVersion.CIRRUS_VERSION;

            // Configure local node version.
            var nodeSettings = NodeSettings.Default(this.Network, ProtocolVersion.CIRRUS_VERSION);
            nodeSettings.MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;

            // Create the ChainIndexer.
            var chain = new ChainIndexer(this.Network);

            // Create behaviour using the test wraper which exposes protected properties and methods
            ILoggerFactory loggerFactory = ExtendedLoggerFactory.Create();
            EnforcePeerVersionCheckBehaviorWrapper behavior = new EnforcePeerVersionCheckBehaviorWrapper(chain, nodeSettings, this.Network, loggerFactory);

            // Intentionally set Peer Version to 0 as its value it shouldn't be used anythere in the test.
            var localPeer = CreateNetworkPeer(0);
            behavior.Attach(localPeer);

            var remotePeer = CreateNetworkPeer(ProtocolVersion.ALT_PROTOCOL_VERSION);

            // Set the initial block height to 1.
            for (int i = 0; i < 4; i++)
            {
                this.AppendBlock(chain);
                behavior.TestOnMessageReceivedAsync(remotePeer, null);
                Assert.Equal(NetworkPeerState.Connected, localPeer.State);
            }

            // Nodes should disconnect when reaching the EnforceMinProtocolVersionAtBlockHeight height.
            this.AppendBlock(chain);
            behavior.TestOnMessageReceivedAsync(remotePeer, null);
            Assert.Equal(NetworkPeerState.Offline, localPeer.State);

            // New connections established after the hard-fork should be disconnected.
            remotePeer = CreateNetworkPeer(ProtocolVersion.ALT_PROTOCOL_VERSION);
            behavior.TestOnMessageReceivedAsync(remotePeer, null);
            Assert.Equal(NetworkPeerState.Offline, localPeer.State);
        }

        [Fact]
        public void CompatibileNodesStayConnectedAfterHardFork()
        {
            // Set the hard-fork parameters.
            this.Network.Consensus.Options.EnforceMinProtocolVersionAtBlockHeight = 5;
            this.Network.Consensus.Options.EnforcedMinProtocolVersion = ProtocolVersion.CIRRUS_VERSION;

            // Configure local node version.
            var nodeSettings = NodeSettings.Default(this.Network, ProtocolVersion.CIRRUS_VERSION);
            nodeSettings.MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;

            // Create the ChainIndexer.
            var chain = new ChainIndexer(this.Network);

            // Create behaviour using the test wraper which exposes protected properties and methods
            ILoggerFactory loggerFactory = ExtendedLoggerFactory.Create();
            EnforcePeerVersionCheckBehaviorWrapper behavior = new EnforcePeerVersionCheckBehaviorWrapper(chain, nodeSettings, this.Network, loggerFactory);

            // Intentionally set Peer Version to 0 as its value it shouldn't be used anythere in the test.
            var localPeer = CreateNetworkPeer(0);
            behavior.Attach(localPeer);

            var remotePeer = CreateNetworkPeer(ProtocolVersion.CIRRUS_VERSION);

            // Set the initial block height to 1.
            for (int i = 0; i < 4; i++)
            {
                this.AppendBlock(chain);
                behavior.TestOnMessageReceivedAsync(remotePeer, null);
                Assert.Equal(NetworkPeerState.Connected, localPeer.State);
            }

            // Nodes shouldn't disconnect when reaching and exceeding the EnforceMinProtocolVersionAtBlockHeight height.
            for (int i = 0; i < 5; i++)
            {
                this.AppendBlock(chain);
                behavior.TestOnMessageReceivedAsync(remotePeer, null);
                Assert.Equal(NetworkPeerState.Connected, localPeer.State);
            }

            // New connections established after the hard-fork should not be disconnected.
            remotePeer = CreateNetworkPeer(ProtocolVersion.CIRRUS_VERSION);
            behavior.TestOnMessageReceivedAsync(remotePeer, null);
            Assert.Equal(NetworkPeerState.Connected, localPeer.State);
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ChainIndexer chain in chainsIndexer)
            {
                Block block = this.Network.CreateBlock();
                block.AddTransaction(this.Network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chainsIndexer);
        }
    }
}