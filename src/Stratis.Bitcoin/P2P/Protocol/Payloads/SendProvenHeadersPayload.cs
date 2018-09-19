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
        /// <see cref="RequireFromHeight"/>
        /// </summary>
        private int requireFromHeight;

        /// <summary>
        /// Gets a height from which proven headers must be sent over the normal header.
        /// Before that height sending normal headers is acceptable because they are checkpointed and therefore
        /// don't require a proof of validity which proven headers supply.
        /// </summary>
        public int RequireFromHeight => this.requireFromHeight;

        /// <inheritdoc />
        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.requireFromHeight);
        }
    }
}