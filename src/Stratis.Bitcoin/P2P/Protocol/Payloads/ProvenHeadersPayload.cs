using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Proven block headers received after a getheaders message.
    /// </summary>
    [Payload("provhdr")]
    public class ProvenHeadersPayload : Payload
    {
        private List<BlockHeader> headers = new List<BlockHeader>();

        public List<BlockHeader> Headers => this.headers;

        public ProvenHeadersPayload()
        {
        }

        public ProvenHeadersPayload(params ProvenBlockHeader[] headers)
        {
            this.Headers.AddRange(headers);
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.headers);
        }
    }
}