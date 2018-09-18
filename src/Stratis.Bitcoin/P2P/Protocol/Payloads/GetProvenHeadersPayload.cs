using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Get proven headers payload which requests proven headers using a similar mechanism as
    /// the getheaders protocol message.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.P2P.Protocol.Payloads.Payload" />
    [Payload("getprovhdr")]
    public class GetProvenHeadersPayload : Payload
    {
        /// <summary>
        /// A block locator which represents a compact structure of one's chain position which can be used to find
        /// forks with another chain.
        /// </summary>
        private BlockLocator blockLocator;

        /// <summary>
        /// Gets a block locator which represents a compact structure of one's chain position which can be used to find
        /// forks with another chain.
        /// </summary>
        public BlockLocator BlockLocator
        {
            get => this.blockLocator;
            set => this.blockLocator = value;
        }

        /// <summary>
        /// Hash of the block after which constructing proven headers payload should stop.
        /// </summary>
        private uint256 hashStop = uint256.Zero;

        /// <summary>
        /// Gets a hash of the block after which constructing proven headers payload should stop.
        /// </summary>
        public uint256 HashStop
        {
            get => this.hashStop;
            set => this.hashStop = value;
        }

        public GetProvenHeadersPayload()
        {
        }

        public GetProvenHeadersPayload(BlockLocator locator)
        {
            this.BlockLocator = locator;
        }

        /// <inheritdoc />
        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.blockLocator);
            stream.ReadWrite(ref this.hashStop);
        }
    }
}
