using Microsoft.AspNetCore.Mvc;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
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
        /// <returns>List of RPC peer nodes</returns>
        [ActionName("getpeerinfo")]
        public List<PeerNodeModel> GetPeerInfo()
        {
            List<PeerNodeModel> peerList = new List<PeerNodeModel>();
            int index = 0;
            foreach (Node node in this.ConnectionManager.ConnectedNodes)
            {
                if (node != null && node.RemoteSocketAddress != null)
                {
                    PeerNodeModel rpcNode = new PeerNodeModel
                    {
                        Id = index,
                        Address = node.RemoteSocketEndpoint.ToString(),
                    };

                    if (node.MyVersion != null)
                    {
                        rpcNode.LocalAddress = node.MyVersion.AddressReceiver?.ToString();
                        rpcNode.Services = ((ulong)node.MyVersion.Services).ToString("X");
                        rpcNode.Version = (uint)node.MyVersion.Version;
                        rpcNode.SubVersion = node.MyVersion.UserAgent;
                        rpcNode.StartingHeight = node.MyVersion.StartHeight;
                    }

                    ConnectionManagerBehavior connectionManagerBehavior = node.Behavior<ConnectionManagerBehavior>();
                    if (connectionManagerBehavior != null)
                    {
                        rpcNode.Inbound = connectionManagerBehavior.Inbound;
                        rpcNode.IsWhiteListed = connectionManagerBehavior.Whitelisted;
                    }

                    ChainHeadersBehavior chainHeadersBehavior = node.Behavior<ChainHeadersBehavior>();
                    if (chainHeadersBehavior != null)
                    {
                        rpcNode.Blocks = chainHeadersBehavior.PendingTip.Height;
                    }

                    if (node.TimeOffset != null)
                    {
                        rpcNode.TimeOffset = node.TimeOffset.Value.Seconds;
                    }

                    peerList.Add(rpcNode);
                }
                index++;
            }

            return peerList;
        }

    }
}
