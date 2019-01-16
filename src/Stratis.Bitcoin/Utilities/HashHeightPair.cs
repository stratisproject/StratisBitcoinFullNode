using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Pair of block hash and block height.</summary>
    public class HashHeightPair : IBitcoinSerializable
    {
        private uint256 hash;

        private int height;

        public uint256 Hash => this.hash;

        public int Height => this.height;

        public HashHeightPair()
        {
        }

        public HashHeightPair(uint256 hash, int height)
        {
            Guard.NotNull(hash, nameof(hash));

            this.hash = hash;
            this.height = height;
        }

        public HashHeightPair(ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            this.hash = chainedHeader.HashBlock;
            this.height = chainedHeader.Height;
        }

        public static HashHeightPair Load(byte[] bytes)
        {
            Guard.NotNull(bytes, nameof(bytes));

            var pair = new HashHeightPair();

            pair.ReadWrite(bytes);

            return pair;
        }

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

        public static bool operator ==(HashHeightPair a, HashHeightPair b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                return false;

            return ((a.hash == b.hash) && (a.height == b.height));
        }

        public static bool operator !=(HashHeightPair a, HashHeightPair b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override bool Equals(object value)
        {
            if (typeof(HashHeightPair) != value.GetType())
                return false;

            return this == (HashHeightPair)value;
        }

        /// <summary>Constructs <see cref="HashHeightPair"/> from a set bytes and the given network.</summary>
        public static HashHeightPair Load(byte[] bytes, Network network)
        {
            var hashHeight = new HashHeightPair();
            hashHeight.ReadWrite(bytes, network.Consensus.ConsensusFactory);
            return hashHeight;
        }
    }
}
