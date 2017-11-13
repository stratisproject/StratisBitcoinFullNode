namespace NBitcoin.Protocol
{
    /// <summary>
    /// Ask block headers that happened since BlockLocators.
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
