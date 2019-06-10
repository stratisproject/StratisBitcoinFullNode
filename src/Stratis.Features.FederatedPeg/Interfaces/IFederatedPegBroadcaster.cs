using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Features.FederatedPeg.Payloads;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Broadcasts a payload to all federated peg nodes, from -federationips.
    /// </summary>
    public interface IFederatedPegBroadcaster
    {
        /// <summary>
        /// Broadcast the given payload to the known federated peg nodes.
        /// </summary>
        Task BroadcastAsync(Payload payload);
    }
}
