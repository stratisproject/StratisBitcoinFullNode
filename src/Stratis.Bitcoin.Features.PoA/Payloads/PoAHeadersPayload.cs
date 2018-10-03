using System.Collections.Generic;
using NBitcoin;
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
        private List<PoABlockHeader> headers;

        public List<PoABlockHeader> Headers
        {
            get { return this.headers; }
        }

        public PoAHeadersPayload()
        {
            this.headers = new List<PoABlockHeader>();
        }

        public PoAHeadersPayload(params PoABlockHeader[] headers)
        {
            this.Headers.AddRange(headers);
        }

        [NoTrace]
        public override void ReadWriteCore(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                stream.ReadWrite(ref this.headers);
            }
            else
            {
                this.headers.Clear();
                stream.ReadWrite(ref this.headers);
            }
        }
    }
}
