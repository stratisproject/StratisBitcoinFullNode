using Microsoft.AspNetCore.Mvc;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public class ConnectionManagerController : BaseRPCController
    {
        public ConnectionManagerController(IConnectionManager connectionManager) : base(connectionManager: connectionManager)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
        }

        [ActionName("addnode")]
        public bool AddNode(string endpointStr, string command)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
            IPEndPoint endpoint = NodeSettings.ConvertToEndpoint(endpointStr, this.ConnectionManager.Network.DefaultPort);
            switch (command)
            {
                case "add":
                    this.ConnectionManager.AddNodeAddress(endpoint);
                    break;
                case "remove":
                    this.ConnectionManager.RemoveNodeAddress(endpoint);
                    break;
                case "onetry":
                    this.ConnectionManager.Connect(endpoint);
                    break;
                default:
                    throw new ArgumentException("command");
            }
            return true;
        }

        /// <summary>
        /// RPC implementation of getpeerinfo
        /// </summary>
        /// <see cref="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <returns>List of connected peer nodes</returns>
        [ActionName("getpeerinfo")]
        public List<PeerNodeModel> GetPeerInfo()
        {
            List<PeerNodeModel> peerList = new List<PeerNodeModel>();

            List<Node> nodes = this.ConnectionManager.ConnectedNodes.ToList();
            foreach (Node node in nodes)
            {
                if (node != null && node.RemoteSocketAddress != null)
                {
                    PeerNodeModel peerNode = new PeerNodeModel
                    {
                        Id = nodes.IndexOf(node),
                        Address = node.RemoteSocketEndpoint.ToString()
                    };

                    if (node.MyVersion != null)
                    {
                        peerNode.LocalAddress = node.MyVersion.AddressReceiver?.ToString();
                        peerNode.Services = ((ulong)node.MyVersion.Services).ToString("X");
                        peerNode.Version = (uint)node.MyVersion.Version;
                        peerNode.SubVersion = node.MyVersion.UserAgent;
                        peerNode.StartingHeight = node.MyVersion.StartHeight;
                    }

                    ConnectionManagerBehavior connectionManagerBehavior = node.Behavior<ConnectionManagerBehavior>();
                    if (connectionManagerBehavior != null)
                    {
                        peerNode.Inbound = connectionManagerBehavior.Inbound;
                        peerNode.IsWhiteListed = connectionManagerBehavior.Whitelisted;
                    }

                    if (node.TimeOffset != null)
                    {
                        peerNode.TimeOffset = node.TimeOffset.Value.Seconds;
                    }

                    peerList.Add(peerNode);
                }
            }

            return peerList;
        }

    }
}
