using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Proven block headers received after a getheaders message.
    /// </summary>
    [Payload("provhdr")]
    public class ProvenHeadersPayload : Payload
    {
        private class ProvenBlockHeaderWithTxCount : IBitcoinSerializable
        {
            internal ProvenBlockHeader Header;

            public ProvenBlockHeaderWithTxCount()
            {
            }

            public ProvenBlockHeaderWithTxCount(ProvenBlockHeader header)
            {
                this.Header = header;
            }

            public void ReadWrite(BitcoinStream stream)
            {
                stream.ReadWrite(ref this.Header);
                var txCount = new VarInt(0);
                stream.ReadWrite(ref txCount);

                // Stratis adds an additional byte to the end of a header need to investigate why.
                if (stream.ConsensusFactory is PosConsensusFactory)
                {
                    stream.ReadWrite(ref txCount);
                }
            }
        }

        public List<ProvenBlockHeader> Headers { get; } = new List<ProvenBlockHeader>();

        public ProvenHeadersPayload()
        {
        }

        public ProvenHeadersPayload(params ProvenBlockHeader[] headers)
        {
            this.Headers.AddRange(headers);
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                List<ProvenBlockHeaderWithTxCount> headersOff = this.Headers.Select(h => new ProvenBlockHeaderWithTxCount(h)).ToList();
                stream.ReadWrite(ref headersOff);
            }
            else
            {
                this.Headers.Clear();
                var headersOff = new List<ProvenBlockHeaderWithTxCount>();
                stream.ReadWrite(ref headersOff);
                this.Headers.AddRange(headersOff.Select(h => h.Header));
            }
        }
    }
}