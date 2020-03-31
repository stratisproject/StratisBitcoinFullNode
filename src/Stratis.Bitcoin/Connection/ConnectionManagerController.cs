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

        private readonly IPeerBanning peerBanning;

        public ConnectionManagerController(IConnectionManager connectionManager,
            ILoggerFactory loggerFactory, IPeerBanning peerBanning) : base(connectionManager: connectionManager)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerBanning = peerBanning;
        }

        /// <summary>
        /// Sends a command to a node.
        /// </summary>
        /// <param name="endpointStr">The endpoint in string format.</param>
        /// <param name="command">The command to run. Three commands are valid: add, remove, and onetry</param>
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
                    if (this.peerBanning.IsBanned(endpoint))
                        throw new InvalidOperationException("Can't perform 'add' for a banned peer.");

                    this.ConnectionManager.AddNodeAddress(endpoint);
                    break;

                case "remove":
                    this.ConnectionManager.RemoveNodeAddress(endpoint);
                    break;

                case "onetry":
                    if (this.peerBanning.IsBanned(endpoint))
                        throw new InvalidOperationException("Can't connect to a banned peer.");

                    this.ConnectionManager.ConnectAsync(endpoint).GetAwaiter().GetResult();
                    break;

                default:
                    throw new ArgumentException("command");
            }

            return true;
        }

        /// <summary>
        /// Sends a command to the connection manager.
        /// </summary>
        /// <param name="endpoint">The endpoint in string format. Specify an IP address. The default port for the network will be added automatically.</param>
        /// <param name="command">The command to run. {add, remove, onetry}</param>
        /// <returns>Json formatted <c>True</c> indicating success. Returns <see cref="IActionResult"/> formatted exception if fails.</returns>
        /// <remarks>This is an API implementation of an RPC call.</remarks>
        /// <exception cref="ArgumentException">Thrown if either command not supported/empty or if endpoint is invalid/empty.</exception>
        /// <response code="200">The node was added</response>
        /// <response code="400">An exception occurred</response>
        [Route("api/[controller]/addnode")]
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
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
        /// <see href="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
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
        /// Gets information about this node.
        /// </summary>
        /// <see href="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <remarks>This is an API implementation of an RPC call.</remarks>
        /// <returns>Json formatted <see cref="List{PeerNodeModel}"/> of connected nodes. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        /// <response code="200">Returns peer information list</response>
        /// <response code="400">Unexpected exception occurred</response>
        [Route("api/[controller]/getpeerinfo")]
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
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
