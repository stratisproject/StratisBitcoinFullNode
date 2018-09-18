using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Send proven headers payload which informs the peer that we are only willing to sync using the proven
    /// headers and not the old type of headers.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.P2P.Protocol.Payloads.Payload" />
    [Payload("sendprovhdr")]
    public class SendProvenHeadersPayload : Payload
    {
        /// <summary>
        /// A height from which proven headers should be received.
        /// </summary>
        private int height;

        /// <summary>
        /// Gets a height from which proven headers should be received.
        /// </summary>
        public int FromHeight => this.height;

        /// <inheritdoc />
        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.height);
        }
    }
}