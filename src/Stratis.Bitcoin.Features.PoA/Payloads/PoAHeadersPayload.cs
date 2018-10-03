using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.PoA.Payloads
{
    /// <summary>
    /// Block headers received after a getheaders messages.
    /// </summary>
    [Payload("poahdr")]
    public class PoAHeadersPayload : Payload
    {
        // TODO POA (similar to HeadersPayload)
    }
}
