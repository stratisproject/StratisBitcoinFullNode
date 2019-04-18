using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Features.Diagnostic.Utils;

namespace Stratis.Features.Diagnostic.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class DiagnosticController : FeatureController
    {
        private ISignals signals;

        public DiagnosticController(ISignals signals, IConsensusManager consensusManager, IConnectionManager connectionManager)
            : base(connectionManager: connectionManager, consensusManager: consensusManager)
        {
        }

        /// <summary>
        /// Returns the connected peers with some information
        /// </summary>
        [HttpGet]
        public IActionResult GetConnectedPeersInfo()
        {
            try
            {
                var peersByPeerId = this.ConsensusManager.GetPrivateFieldValue<Dictionary<int, INetworkPeer>>("peersByPeerId").Values.ToList();
                var connectedPeers = this.ConnectionManager.ConnectedPeers.ToList();

                object DumpPeer(INetworkPeer peer)
                {
                    return new { peer.IsConnected, peer.DisconnectReason, peer.State, EndPoint = peer.PeerEndPoint.ToString() };
                }

                return this.Json(new
                {
                    peersByPeerId = peersByPeerId.Select(DumpPeer),
                    connectedPeers = connectedPeers.Select(DumpPeer),
                    connectedPeersNotInPeersByPeerId = connectedPeers.Except(peersByPeerId).Select(DumpPeer)
                });
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
