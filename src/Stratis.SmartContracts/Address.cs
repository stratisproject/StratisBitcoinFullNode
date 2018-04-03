namespace Stratis.SmartContracts
{
    /// <summary>
    /// Helper struct that represents a STRAT address.
    /// <para>
    /// This struct is used when sending or receiving funds.
    /// </para>
    /// </summary>
    public struct Address
    {
        /// <summary>
        /// The address as a string, in base58 format.
        /// </summary>
        public string Value;

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