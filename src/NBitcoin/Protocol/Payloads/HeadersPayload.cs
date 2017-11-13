using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Protocol
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

            #region IBitcoinSerializable Members

            public void ReadWrite(BitcoinStream stream)
            {
                stream.ReadWrite(ref this.Header);
                VarInt txCount = new VarInt(0);
                stream.ReadWrite(ref txCount);

                // Stratis adds an additional byte to the end of a header need to investigate why.
                if (Transaction.TimeStamp)
                    stream.ReadWrite(ref txCount);
            }

            #endregion
        }

        private List<BlockHeader> headers = new List<BlockHeader>();
        public List<BlockHeader> Headers { get { return this.headers; } }

        public HeadersPayload()
        {
        }

        public HeadersPayload(params BlockHeader[] headers)
        {
            this.Headers.AddRange(headers);
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                List<BlockHeaderWithTxCount> heardersOff = this.headers.Select(h => new BlockHeaderWithTxCount(h)).ToList();
                stream.ReadWrite(ref heardersOff);
            }
            else
            {
                this.headers.Clear();
                List<BlockHeaderWithTxCount> headersOff = new List<BlockHeaderWithTxCount>();
                stream.ReadWrite(ref headersOff);
                this.headers.AddRange(headersOff.Select(h => h.Header));
            }
        }
    }
}