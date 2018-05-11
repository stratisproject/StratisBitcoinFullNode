using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public class ConnectionManagerController : FeatureController
    {
        public ConnectionManagerController(IConnectionManager connectionManager) : base(connectionManager: connectionManager)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
        }

        [ActionName("addnode")]
        [ActionDescription("Adds a node to the connection manager.")]
        public bool AddNode(string endpointStr, string command)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
            IPEndPoint endpoint = NodeSettings.ConvertIpAddressToEndpoint(endpointStr, this.ConnectionManager.Network.DefaultPort);
            switch (command)
            {
                case "add":
                    this.ConnectionManager.AddNodeAddress(endpoint);
                    break;

                case "remove":
                    this.ConnectionManager.RemoveNodeAddress(endpoint);
                    break;

                case "onetry":
                    this.ConnectionManager.ConnectAsync(endpoint).GetAwaiter().GetResult();
                    break;

                default:
                    throw new ArgumentException("command");
            }

            return true;
        }

        /// <summary>
        /// RPC implementation of "getpeerinfo".
        /// </summary>
        /// <see cref="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <returns>List of connected peer nodes.</returns>
        [ActionName("getpeerinfo")]
        [ActionDescription("Gets peer information from the connection manager.")]
        public List<PeerNodeModel> GetPeerInfo()
        {
            List<PeerNodeModel> peerList = new List<PeerNodeModel>();

            List<INetworkPeer> peers = this.ConnectionManager.ConnectedPeers.ToList();
            foreach (INetworkPeer peer in peers)
            {
                if ((peer != null) && (peer.RemoteSocketAddress != null))
                {
                    PeerNodeModel peerNode = new PeerNodeModel
                    {
                        Id = peers.IndexOf(peer),
                        Address = peer.RemoteSocketEndpoint.ToString()
                    };

                    if (peer.MyVersion != null)
                    {
                        peerNode.LocalAddress = peer.MyVersion.AddressReceiver?.ToString();
                        peerNode.Services = ((ulong)peer.MyVersion.Services).ToString("X");
                        peerNode.Version = (uint)peer.MyVersion.Version;
                        peerNode.SubVersion = peer.MyVersion.UserAgent;
                        peerNode.StartingHeight = peer.MyVersion.StartHeight;
                    }

                    ConnectionManagerBehavior connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                    if (connectionManagerBehavior != null)
                    {
                        peerNode.Inbound = connectionManagerBehavior.Inbound;
                        peerNode.IsWhiteListed = connectionManagerBehavior.Whitelisted;
                    }

                    if (peer.TimeOffset != null)
                    {
                        peerNode.TimeOffset = peer.TimeOffset.Value.Seconds;
                    }

                    peerList.Add(peerNode);
                }
            }

            return peerList;
        }
    }
}
