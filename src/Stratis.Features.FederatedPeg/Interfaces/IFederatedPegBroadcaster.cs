using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Features.FederatedPeg.Payloads;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Broadcasts a payload to all of the other federated peg nodes.
    /// </summary>
    public interface IFederatedPegBroadcaster
    {
        Task BroadcastAsync(Payload payload);
    }
}
