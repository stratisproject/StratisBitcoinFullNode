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
        /// <see cref="BlockLocator"/>
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
        /// <see cref="HashStop"/>
        /// </summary>
        private uint256 hashStop;

        /// <summary>
        /// Gets a hash after which no new headers should be sent withing the same message.
        /// </summary>
        /// <remarks>
        /// As an example, in case we are asked to send headers from block 1000 but hashStop is at block
        /// 1200 the answer should contain 200 headers.
        /// </remarks>
        public uint256 HashStop
        {
            get => this.hashStop;
            set => this.hashStop = value;
        }

        public GetProvenHeadersPayload()
        {
            this.HashStop = uint256.Zero;
        }

        public GetProvenHeadersPayload(BlockLocator locator)
        {
            this.BlockLocator = locator;
            this.HashStop = uint256.Zero;
        }

        /// <inheritdoc />
        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.blockLocator);
            stream.ReadWrite(ref this.hashStop);
        }
    }
}
