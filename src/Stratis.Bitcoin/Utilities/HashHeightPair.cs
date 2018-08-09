using System;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Pair of block hash and block height.</summary>
    public class HashHeightPair : IBitcoinSerializable
    {
        public HashHeightPair()
        {
        }

        public HashHeightPair(uint256 hash, int height)
        {
            Guard.NotNull(hash, nameof(hash));

            this.hash = hash;
            this.height = height;
        }

        public uint256 Hash
        {
            get => this.hash;
            set => this.hash = value;
        }

        public int Height
        {
            get => this.height;
            set => this.height = value;
        }

        private uint256 hash;

        private int height;

        /// <inheritdoc />
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.hash);
            stream.ReadWrite(ref this.height);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.height + "-" + this.hash;
        }

        public static HashHeightPair Load(byte[] hex)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            var pair = new HashHeightPair();

            pair.ReadWrite(hex);

            return pair;
        }
    }
}
