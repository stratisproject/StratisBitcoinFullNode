using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoABlockHeader : BlockHeader
    {
        private BlockSignature federationSignature;

        public BlockSignature FederationSignature
        {
            get => this.federationSignature;
            set => this.federationSignature = value;
        }

        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
            stream.ReadWrite(ref this.federationSignature);
        }
    }
}
