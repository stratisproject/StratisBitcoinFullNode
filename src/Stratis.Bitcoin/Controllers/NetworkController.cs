using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Provides methods that interact with the network elements of the full node.
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    public sealed class NetworkController : Controller
    {
        /// <summary>The connection manager.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The network the node is running on.</summary>
        private readonly Network network;

        /// <summary>Network peer banning behaviour.</summary>
        private readonly IPeerBanning peerBanning;

        public NetworkController(
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            Network network,
            IPeerBanning peerBanning)
        {
            this.connectionManager = connectionManager;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerBanning = peerBanning;
        }

        /// <summary>
        /// Disconnects a connected peer.
        /// </summary>
        /// <param name="viewModel">The model that represents the peer to disconnect.</param>
        /// <returns><see cref="OkResult"/></returns>
        /// <response code="200">Peer disconnected</response>
        /// <response code="400">Unexpected exception occurred</response>
        [Route("disconnect")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public IActionResult DisconnectPeer([FromBody] DisconnectPeerViewModel viewModel)
        {
            try
            {
                var endpoint = viewModel.PeerAddress.ToIPEndPoint(this.network.DefaultPort);
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
        /// <returns><see cref="OkResult"/></returns>
        /// <response code="200">Ban status updated</response>
        /// <response code="400">An exception occurred</response>
        [Route("setban")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public IActionResult SetBan([FromBody] SetBanPeerViewModel viewModel)
        {
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(viewModel.PeerAddress), this.network.DefaultPort);
                switch (viewModel.BanCommand.ToLowerInvariant())
                {
                    case "add":
                        {
                            int banDuration = this.connectionManager.ConnectionSettings.BanTimeSeconds;
                            if (viewModel.BanDurationSeconds != null && viewModel.BanDurationSeconds.Value > 0)
                                banDuration = viewModel.BanDurationSeconds.Value;

                            this.peerBanning.BanAndDisconnectPeer(endpoint, banDuration, "Banned via the API.");

                            break;
                        }

                    case "remove":
                        {
                            this.peerBanning.UnBanPeer(endpoint);
                            break;
                        }

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
        /// Retrieves a list of all banned peers.
        /// </summary>
        /// <returns>List of banned peers</returns>
        /// <response code="200">Returns banned peers</response>
        /// <response code="400">Unexpected exception occurred</response>
        [Route("getbans")]
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public IActionResult GetBans()
        {
            try
            {
                List<PeerAddress> allBannedPeers = this.peerBanning.GetAllBanned();

                return this.Json(allBannedPeers.Select(p => new BannedPeerModel() { EndPoint = p.Endpoint.ToString(), BanUntil = p.BanUntil, BanReason = p.BanReason }));
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
        /// <param name="corsProtection">This body parameter is here to prevent a CORS call from triggering method execution.</param>
        /// <remarks>
        /// <seealso href="https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS#Simple_requests"/>
        /// </remarks>
        /// <returns><see cref="OkResult"/></returns>
        /// <response code="200">Bans cleared</response>
        /// <response code="400">Unexpected exception occurred</response>
        [Route("clearbanned")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public IActionResult ClearBannedPeers([FromBody] bool corsProtection = true)
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
