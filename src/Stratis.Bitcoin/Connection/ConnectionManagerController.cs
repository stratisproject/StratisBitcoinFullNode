using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Connection
{
    [Route("api/[controller]")]
    public class ConnectionManagerController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ConnectionManagerController(IConnectionManager connectionManager,
            ILoggerFactory loggerFactory) : base(connectionManager: connectionManager)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("addnode")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionDescription("Adds a node to the connection manager.")]
        public bool AddNode(string endpointStr, string command)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
            IPEndPoint endpoint = endpointStr.ToIPEndPoint(this.ConnectionManager.Network.DefaultPort);
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
        /// Adds a node to the connection manager.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="AddNodeRequestModel"/> formatted request containing an endpoint and command.</param>
        /// <returns>Json formatted <c>True</c> indicating success. Returns <see cref="IActionResult"/> formatted exception if fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either request.Endpoint or request.Command are null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if request.Command is invalid/not supported.</exception>
        [Route("addnode")]
        [HttpGet]
        public IActionResult AddNode(AddNodeRequestModel request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Endpoint))
                {
                    throw new ArgumentNullException("Endpoint");
                }

                if (string.IsNullOrEmpty(request.Command))
                {
                    throw new ArgumentNullException("Command");
                }

                return this.Json(this.AddNode(request.Endpoint, request.Command));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// RPC implementation of "getpeerinfo".
        /// </summary>
        /// <see cref="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <returns>List of connected peer nodes.</returns>
        [ActionName("getpeerinfo")]
        [ApiExplorerSettings(IgnoreApi = true)]
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

        /// <summary>
        /// Gets peer information from the connection manager.
        /// API implementation of RPC call.
        /// </summary>
        /// <see cref="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <returns>Json formatted <see cref="List{T}<see cref="Models.PeerNodeModel"/>"/> of connected nodes. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getpeerinfo")]
        [HttpGet]
        public IActionResult GetPeerInfoAPI()
        {
            try
            {
                return this.Json(this.GetPeerInfo());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
