namespace Stratis.SmartContracts
{
    /// <summary>
    /// This is only really used to aid Smart Contract Developers' understanding of addresses.
    /// They may not easily understand the idea of sending to a uint160
    /// </summary>
    public struct Address
    {
        public string Value;

        public Address(string address)
        {
            this.Value = address;
        }

        public static readonly Address Zero = new Address("0x0000000000000000000000000000000000000000");
    
        public override string ToString()
        {
            return this.Value;
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
