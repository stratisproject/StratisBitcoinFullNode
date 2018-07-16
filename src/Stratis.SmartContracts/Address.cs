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
        public string Value;

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
            return obj1.Value == obj2.Value;
        }

        public static bool operator !=(Address obj1, Address obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            return this == (Address)obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}