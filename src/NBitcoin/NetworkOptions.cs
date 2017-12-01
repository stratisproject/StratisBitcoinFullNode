namespace NBitcoin
{
    /// <summary>
    /// This class is a drop-in replacement for the legacy TransactionOptions enumeration.
    /// </summary>
    public class NetworkOptions
    {
        public const uint None = 0x00000000;
        public const uint Signature = 0x10000000;
        public const uint TimeStamp = 0x20000000;
        public const uint Witness = 0x40000000;
        public const uint All = Witness;
        public const uint POS = Signature | TimeStamp;
        public const uint POSAll = Witness | POS;

        private uint flags = All;

        /// <summary>
        /// Creates a NetworkOptions object.
        /// </summary>
        /// <param name="flags">The flags to maintain in the object.</param>
        public NetworkOptions(uint flags = All)
        {
            this.flags = flags;
        }

        /// <summary>
        /// Clones the NetworkOptions object.
        /// </summary>
        /// <returns>The cloned NetworkOptions object.</returns>
        public NetworkOptions Clone()
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
            if (left == null)
                Swap(ref left, ref right); 
            NetworkOptions clone = left?.Clone();
            if (clone != null)
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
            if (left == null)
                Swap(ref left, ref right); 
            NetworkOptions clone = left?.Clone();
            if (clone != null)
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
        /// Compares two NetworkOptions objects for the equality.
        /// </summary>
        /// <param name="left">The left NetworkOptions object.</param>
        /// <param name="right">The right NetworkOptions object.</param>
        /// <returns>Returns true iff the two objects are the same.</returns>
        public override bool Equals(object obj)
        {
            return (uint)(obj as NetworkOptions) == (uint)this;
        }

        /// <summary>
        /// Calculates the hash code of this object.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return (int)(uint)this;
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
