using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <inheritdoc />
    /// <summary>
    /// Get proven headers payload which requests proven headers using a similar mechanism as
    /// the getheaders protocol message.
    /// </summary>
    /// <seealso cref="T:Stratis.Bitcoin.P2P.Protocol.Payloads.Payload" />
    [Payload("getprovhdr")]
    public class GetProvenHeadersPayload : GetHeadersPayload
    {
        public GetProvenHeadersPayload()
        {
            base.HashStop = uint256.Zero;
        }

        public GetProvenHeadersPayload(BlockLocator locator)
        {
            base.BlockLocator = locator;
            base.HashStop = uint256.Zero;
        }

        /// <inheritdoc />
        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref base.blockLocator);
            stream.ReadWrite(ref base.hashStop);
        }
    }
}
