using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoABlockHeader : BlockHeader
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
            stream.ReadWrite(ref this.blockSignature);
        }

        /// <summary>
        /// Generates the hash of a <see cref="PoABlockHeader"/>.
        /// </summary>
        /// <returns>A hash.</returns>
        public override uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] hashes = this.hashes;

            if (hashes != null)
                hash = hashes[0];

            if (hash != null)
                return hash;

            using (var hs = new HashStream())
            {
                // We are using base serialization to avoid using signature during hash calculation.
                base.ReadWrite(new BitcoinStream(hs, true));
                hash = hs.GetHash();
            }

            hashes = this.hashes;

            if (hashes != null)
                hashes[0] = hash;

            return hash;
        }
    }
}
