namespace Stratis.SmartContracts
{
    /// <summary>
    /// Helper struct that wraps uint160 operations.
    /// <para>
    /// This struct makes it easier to semantically understand send and receive addresses.
    /// </para>
    /// </summary>
    public struct Address
    {
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