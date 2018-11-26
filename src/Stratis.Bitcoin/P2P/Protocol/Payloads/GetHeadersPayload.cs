using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Ask block headers that happened since BlockLocator.
    /// </summary>
    [Payload("getheaders")]
    public class GetHeadersPayload : Payload
    {
        private uint version = (uint)ProtocolVersion.PROTOCOL_VERSION;

        public ProtocolVersion Version
        {
            get => (ProtocolVersion)this.version;

            set => this.version = (uint)value;
        }

        protected BlockLocator blockLocator;

        /// <summary>
        /// Gets a block locator which represents a compact structure of one's chain position which can be used to find
        /// forks with another chain.
        /// </summary>
        public BlockLocator BlockLocator
        {
            get => this.blockLocator;

            set => this.blockLocator = value;
        }

        protected uint256 hashStop = uint256.Zero;

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

        public GetHeadersPayload()
        {
        }

        public GetHeadersPayload(BlockLocator locator)
        {
            this.BlockLocator = locator;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.blockLocator);
            stream.ReadWrite(ref this.hashStop);
        }
    }
}
