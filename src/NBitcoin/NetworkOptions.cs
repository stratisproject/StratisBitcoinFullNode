namespace NBitcoin
{
    /// <summary>
    /// The current use-case for this class is to provide option-dependent serialization.
    /// It is a drop-in replacement for the legacy TransactionOptions enumeration.
    /// </summary>
    /// <remarks>
    /// The NetworkOptions instance is intended to be relayed from the Network object to 
    /// the child objects: Network => Block => Transaction => TxIn/TxOut => Coin => ...
    /// It can also be used on repository or stream objects that need to deserialize the 
    /// above objects or passed as a parameter futher down the call-hierarchy as needed.
    /// </remarks>
    public class NetworkOptions
    {
        public const uint None = 0x00000000;
        public const uint Witness = 0x40000000;
        public const uint All = Witness;

        /// <summary>ProofOfStake flags.</summary>
        private bool isProofOfStake = false;

        private uint flags = All;

        /// <summary>TODO: To be used as placeholder until static flags have been completely removed - i.e. from the tests as well...</summary>
        public static NetworkOptions TemporaryOptions
        {
            get
            {
                return new NetworkOptions() ;
            }
        }

        /// <summary>
        /// Creates a NetworkOptions object.
        /// </summary>
        /// <param name="flags">The flags to maintain in the object.</param>
        public NetworkOptions(uint flags = All)
        {
            this.flags = flags;
        }

        /// <summary>
        /// Get/Set Witness flag.
        /// </summary>
        public bool IsWitness
        {
            get
            {
                return (this.flags & Witness) != 0;
            }
            set
            {
                this.flags = value ? (this.flags | Witness) : (this.flags & ~Witness);
            }
        }

        /// <summary>
        /// Clones the NetworkOptions object.
        /// </summary>
        /// <returns>The cloned NetworkOptions object.</returns>
        public virtual NetworkOptions Clone()
        {
            var clone = new NetworkOptions();
            clone.flags = this.flags;
            return clone;
        }

        /// <summary>
        /// Allows this object to be explicitly cast to a uint that contains the flags.
        /// </summary>
        /// <param name="o">The NetworkOptions object to cast.</param>
        public static explicit operator uint(NetworkOptions o)
        {
            return o?.flags ?? All;
        }

        /// <summary>
        /// Allows implicitly casting a uint representing flags to a NetworkOptions object.
        /// </summary>
        /// <param name="flags">The uint to cast.</param>
        public static implicit operator NetworkOptions(uint flags)
        {
            return new NetworkOptions(flags);
        }

        /// <summary>
        /// Provides the '|' (or) operator between two NetworkOptions objects.
        /// </summary>
        /// <param name="left">The left NetworkOptions object.</param>
        /// <param name="right">The right NetworkOptions object.</param>
        /// <returns>A NetworkOptions object that represents the union of the input object.</returns>
        public static NetworkOptions operator |(NetworkOptions left, NetworkOptions right)
        {
            if (ReferenceEquals(null, left))
                Swap(ref left, ref right); 
            NetworkOptions clone = left?.Clone();
            if (!ReferenceEquals(null, clone))
                clone.flags |= (right?.flags ?? All);
            return clone;
        }

        /// <summary>
        /// Provides the '&' (and) operator between two NetworkOptions objects.
        /// </summary>
        /// <param name="left">The left NetworkOptions object.</param>
        /// <param name="right">The right NetworkOptions object.</param>
        /// <returns>A NetworkOptions object that represents the intersection of the input object.</returns>
        public static NetworkOptions operator &(NetworkOptions left, NetworkOptions right)
        {
            if (ReferenceEquals(null, left))
                return right?.Clone(); 
            NetworkOptions clone = left.Clone();
            clone.flags &= (right?.flags ?? All);
            return clone;
        }

        /// <summary>
        /// Compares two NetworkOptions objects for the equality of their flags.
        /// </summary>
        /// <param name="left">The left NetworkOptions object.</param>
        /// <param name="right">The right NetworkOptions object.</param>
        /// <returns>Returns true iff the two objects have the same flags.</returns>
        public static bool operator ==(NetworkOptions left, NetworkOptions right)
        {
            return (uint)left == (uint)right;
        }

        /// <summary>
        /// Compares two NetworkOptions objects for the inequality of their flags.
        /// </summary>
        /// <param name="left">The left NetworkOptions object.</param>
        /// <param name="right">The right NetworkOptions object.</param>
        /// <returns>Returns true iff the two objects have the different flags.</returns>
        public static bool operator !=(NetworkOptions left, NetworkOptions right)
        {
            return (uint)left != (uint)right;
        }

        /// <summary>
        /// Swaps two variables.
        /// </summary>
        /// <param name="x">The first variable.</param>
        /// <param name="y">The second variable.</param>
        private static void Swap<T>(ref T x, ref T y)
        {
            T t = y;
            y = x;
            x = t;
        }
    }
}
