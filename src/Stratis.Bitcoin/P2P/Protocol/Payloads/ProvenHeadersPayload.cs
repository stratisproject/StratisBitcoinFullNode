using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Proven headers payload which contains list of up to 2000 proven headers.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.P2P.Protocol.Payloads.Payload" />
    [Payload("provhdr")]
    public class ProvenHeadersPayload : Payload
    {
        /// <summary>
        /// <see cref="Headers"/>
        /// </summary>
        private List<ProvenBlockHeader> headers = new List<ProvenBlockHeader>();

        /// <summary>
        /// Gets a list of up to 2,000 proven headers.
        /// </summary>
        public List<ProvenBlockHeader> Headers => this.headers;

        public ProvenHeadersPayload()
        {
        }

        public ProvenHeadersPayload(params ProvenBlockHeader[] headers)
        {
            this.Headers.AddRange(headers);
        }

        /// <inheritdoc />
        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ConsensusFactory = new ProvenHeaderConsensusFactory();
            stream.ReadWrite(ref this.headers);
        }
    }
}