using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Ask for the block hashes (inv) that happened since BlockLocator.
    /// </summary>
    [Payload("getblocks")]
    public class GetBlocksPayload : Payload
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

        private BlockLocator blockLocators;

        public BlockLocator BlockLocators
        {
            get
            {
                return this.blockLocators;
            }

            set
            {
                this.blockLocators = value;
            }
        }

        private uint256 hashStop = uint256.Zero;

        public uint256 HashStop { get { return this.hashStop; } set { this.hashStop = value; } }

        public GetBlocksPayload()
        {
        }

        public GetBlocksPayload(BlockLocator locator)
        {
            this.BlockLocators = locator;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.blockLocators);
            stream.ReadWrite(ref this.hashStop);
        }
    }
}
