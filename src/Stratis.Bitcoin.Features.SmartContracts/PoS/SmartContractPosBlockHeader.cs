using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS
{
    public sealed class SmartContractPosBlockHeader : SmartContractBlockHeader
    {
        /// <inheritdoc />
        public override int CurrentVersion => 7;

        public SmartContractPosBlockHeader() : base()
        {
        }

        /// <inheritdoc />
        public override uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] innerHashes = this.hashes;

            if (innerHashes != null)
                hash = innerHashes[0];

            if (hash != null)
                return hash;

            if (this.version > 6)
                hash = Hashes.Hash256(this.ToBytes());
            else
                hash = this.GetPoWHash();

            innerHashes = this.hashes;
            if (innerHashes != null)
                innerHashes[0] = hash;

            return hash;
        }

        /// /// <inheritdoc />
        public override uint256 GetPoWHash()
        {
            return HashX13.Instance.Hash(this.ToBytes());
        }
    }
}