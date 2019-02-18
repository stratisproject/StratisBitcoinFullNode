﻿using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Provides methods that interact with the network elements of the full node.
    /// </summary>
    [Route("api/[controller]")]
    public sealed class NetworkController : Controller
    {
        /// <summary>The connection manager.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Network peer banning behaviour.</summary>
        private readonly IPeerBanning peerBanning;

        public NetworkController(
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            IPeerBanning peerBanning)
        {
            this.connectionManager = connectionManager;
            this.peerBanning = peerBanning;
        }

        /// <summary>
        /// Disconnects a connected peer.
        /// </summary>
        /// <param name="viewModel">The model that represents the peer to disconnect.</param>
        [Route("disconnect")]
        [HttpPost]
        public IActionResult DisconnectPeer([FromBody] DisconnectPeerViewModel viewModel)
        {
            try
            {
                var endpoint = viewModel.PeerAddress.ToIPEndPoint(0);
                INetworkPeer peer = this.connectionManager.ConnectedPeers.FindByEndpoint(endpoint);
                if (peer != null)
                {
                    var peerBehavior = peer.Behavior<IConnectionManagerBehavior>();
                    peer.Disconnect($"{endpoint} was disconnected via the API.");
                }

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Adds or remove a peer from the node's banned peers list.
        /// </summary>
        /// <param name="viewModel">The model that represents the peer to add or remove from the banned list.</param>
        [Route("setban")]
        [HttpPost]
        public IActionResult SetBan([FromBody] SetBanPeerViewModel viewModel)
        {
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(viewModel.PeerAddress), 0);

                switch (viewModel.BanCommand.ToLowerInvariant())
                {
                    case "add":
                        this.peerBanning.BanAndDisconnectPeer(endpoint, viewModel.BanDurationSeconds, "Banned via the API.");
                        break;
                    case "remove":
                        this.peerBanning.UnBanPeer(endpoint);
                        break;
                    default:
                        throw new Exception("Only 'add' or 'remove' are valid 'setban' commands.");
                }

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Clears the node of all banned peers.
        /// </summary>
        [Route("clearbanned")]
        [HttpPost]
        public IActionResult ClearBannedPeers()
        {
            try
            {
                this.peerBanning.ClearBannedPeers();

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

    }
}
