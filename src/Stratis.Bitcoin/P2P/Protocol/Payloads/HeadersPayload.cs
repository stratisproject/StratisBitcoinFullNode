using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Block headers received after a getheaders messages.
    /// </summary>
    [Payload("headers")]
    public class HeadersPayload : Payload
    {
        private class BlockHeaderWithTxCount : IBitcoinSerializable
        {
            internal BlockHeader Header;

            public BlockHeaderWithTxCount()
            {
            }

            public BlockHeaderWithTxCount(BlockHeader header)
            {
                this.Header = header;
            }

            [NoTrace]
            public void ReadWrite(BitcoinStream stream)
            {
                stream.ReadWrite(ref this.Header);
                var txCount = new VarInt(0);
                stream.ReadWrite(ref txCount);

                // Stratis adds an additional byte to the end of a header need to investigate why.
                if (stream.ConsensusFactory is PosConsensusFactory)
                    stream.ReadWrite(ref txCount);
            }
        }

        private List<BlockHeader> headers = new List<BlockHeader>();

        public List<BlockHeader> Headers { get { return this.headers; } }

        public HeadersPayload()
        {
        }

        public HeadersPayload(IEnumerable<BlockHeader> headers)
        {
            this.Headers.AddRange(headers);
        }

        [NoTrace]
        public override void ReadWriteCore(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                List<BlockHeaderWithTxCount> headersOff = this.headers.Select(h => new BlockHeaderWithTxCount(h)).ToList();
                stream.ReadWrite(ref headersOff);
            }
            else
            {
                this.headers.Clear();
                var headersOff = new List<BlockHeaderWithTxCount>();
                stream.ReadWrite(ref headersOff);
                this.headers.AddRange(headersOff.Select(h => h.Header));
            }
        }
    }
}