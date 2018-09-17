using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Ask for proven block headers that happened since <see cref="BlockLocator"/>.
    /// </summary>
    [Payload("getprovhdr")]
    public class GetProvenHeadersPayload : Payload
    {
        private BlockLocator blockLocator;

        public BlockLocator BlockLocator
        {
            get => this.blockLocator;
            set => this.blockLocator = value;
        }

        private uint256 hashStop = uint256.Zero;

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

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.blockLocator);
            stream.ReadWrite(ref this.hashStop);
        }
    }
}
