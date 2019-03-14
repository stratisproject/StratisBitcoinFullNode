using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// A <see cref="FeatureController"/> that implements API and RPC methods for the connection manager.
    /// </summary>
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

        /// <summary>
        /// RPC method for adding a node connection.
        /// </summary>
        /// <param name="endpointStr">The endpoint in string format.</param>
        /// <param name="command">The command to run. {add, remove, onetry}</param>
        /// <returns><c>true</c> if successful.</returns>
        /// <exception cref="ArgumentException">Thrown if unsupported command given.</exception>
        [ActionName("addnode")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionDescription("Adds a node to the connection manager.")]
        public bool AddNodeRPC(string endpointStr, string command)
        {
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
        /// API wrapper for RPC call.
        /// </summary>
        /// <param name="endpoint">The endpoint in string format.</param>
        /// <param name="command">The command to run. {add, remove, onetry}</param>
        /// <returns>Json formatted <c>True</c> indicating success. Returns <see cref="IActionResult"/> formatted exception if fails.</returns>
        /// <exception cref="ArgumentException">Thrown if either command not supported/empty or if endpoint is invalid/empty.</exception>
        [Route("api/[controller]/addnode")]
        [HttpGet]
        public IActionResult AddNodeAPI([FromQuery] string endpoint, string command)
        {
            try
            {
                Guard.NotEmpty(endpoint, nameof(endpoint));
                Guard.NotEmpty(command, nameof(command));

                return this.Json(this.AddNodeRPC(endpoint, command));
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
        /// <returns>List of connected peer nodes as <see cref="PeerNodeModel"/>.</returns>
        [ActionName("getpeerinfo")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionDescription("Gets peer information from the connection manager.")]
        public List<PeerNodeModel> GetPeerInfoRPC()
        {
            var peerList = new List<PeerNodeModel>();

            List<INetworkPeer> peers = this.ConnectionManager.ConnectedPeers.ToList();
            foreach (INetworkPeer peer in peers)
            {
                if ((peer != null) && (peer.RemoteSocketAddress != null))
                {
                    var peerNode = new PeerNodeModel
                    {
                        Id = peers.IndexOf(peer),
                        Address = peer.RemoteSocketEndpoint.ToString()
                    };

                    if (peer.PeerVersion != null)
                    {
                        peerNode.LocalAddress = peer.PeerVersion.AddressReceiver?.ToString();
                        peerNode.Services = ((ulong)peer.PeerVersion.Services).ToString("X");
                        peerNode.Version = (uint)peer.PeerVersion.Version;
                        peerNode.SubVersion = peer.PeerVersion.UserAgent;
                        peerNode.StartingHeight = peer.PeerVersion.StartHeight;
                    }

                    var connectionManagerBehavior = peer.Behavior<IConnectionManagerBehavior>();
                    if (connectionManagerBehavior != null)
                    {
                        peerNode.Inbound = peer.Inbound;
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
        /// API wrapper for RPC call.
        /// </summary>
        /// <see cref="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <returns>Json formatted <see cref="List{T}<see cref="PeerNodeModel"/>"/> of connected nodes. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("api/[controller]/getpeerinfo")]
        [HttpGet]
        public IActionResult GetPeerInfoAPI()
        {
            try
            {
                return this.Json(this.GetPeerInfoRPC());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
