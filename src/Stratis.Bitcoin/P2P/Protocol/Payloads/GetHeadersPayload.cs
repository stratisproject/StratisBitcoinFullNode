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
            get
            {
                return (ProtocolVersion)this.version;
            }
            set
            {
                this.version = (uint)value;
            }
        }

        private BlockLocator blockLocator;
        public BlockLocator BlockLocator
        {
            get
            {
                return this.blockLocator;
            }
            set
            {
                this.blockLocator = value;
            }
        }

        private uint256 hashStop = uint256.Zero;
        public uint256 HashStop
        {
            get
            {
                return this.hashStop;
            }
            set
            {
                this.hashStop = value;
            }
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
