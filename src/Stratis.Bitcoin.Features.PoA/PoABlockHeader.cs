using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.PoA
{
#pragma warning disable 618
    public class PoABlockHeader : BlockHeader
#pragma warning restore 618
    {
        private BlockSignature blockSignature;

        public BlockSignature BlockSignature
        {
            get => this.blockSignature;
            set => this.blockSignature = value;
        }

        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);

            // Adding the signature to header because it will be needed for header validation on PoA networks.
            stream.ReadWrite(ref this.blockSignature);
        }

        protected override uint256 CalculateHash()
        {
            using (var hs = new HashStream())
            {
                // We are using base serialization to avoid using signature during hash calculation.
                base.ReadWrite(new BitcoinStream(hs, true));
                uint256 hash = hs.GetHash();
                return hash;
            }
        }
    }
}
