namespace Stratis.SmartContracts
{
    /// <summary>
    /// Helper struct that represents a STRAT address and is used when sending or receiving funds.
    /// <para>
    /// Note that the format of the address is not validated on construction, but when trying to send funds to this address.
    /// </para>
    /// </summary>
    public struct Address
    {
        /// <summary>
        /// The address as a string, in base58 format.
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Create a new address
        /// </summary>
        public Address(string address)
        {
            this.Value = address;
        }

        public override string ToString()
        {
            return this.Value;
        }

        public static explicit operator Address(string value)
        {
            return new Address(value);
        }

        public static implicit operator string(Address x)
        {
            return x.Value;
        }

        public static bool operator ==(Address obj1, Address obj2)
        {
            return obj1.Equals(obj2);
        }

        public static bool operator !=(Address obj1, Address obj2)
        {
            return !obj1.Equals(obj2);
        }

        public override bool Equals(object obj)
        {
            if (obj is Address other)
            {
                return this.Equals(other);
            }

            return false;
        }

        public bool Equals(Address obj)
        {
            return this.Value == obj.Value;
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }
    }
}