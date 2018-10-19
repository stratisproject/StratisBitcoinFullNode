using System;

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
        private readonly string addressString;
        private readonly uint pn0;
        private readonly uint pn1;
        private readonly uint pn2;
        private readonly uint pn3;
        private readonly uint pn4;

        public const int Width = 160 / 8;
        public readonly byte[] Bytes;

        public Address(Address other)
        {
            this.pn0 = other.pn0;
            this.pn1 = other.pn1;
            this.pn2 = other.pn2;
            this.pn3 = other.pn3;
            this.pn4 = other.pn4;
            this.Bytes = other.Bytes;
            this.addressString = other.addressString;
        }

        private Address(uint pn0, uint pn1, uint pn2, uint pn3, uint pn4, byte[] bytes, string str)
        {
            this.pn0 = pn0;
            this.pn1 = pn1;
            this.pn2 = pn2;
            this.pn3 = pn3;
            this.pn4 = pn4;
            this.addressString = str;
            this.Bytes = bytes;
        }
        
        internal static Address Create(byte[] bytes, string str)
        {
            var pn0 = ToUInt32(bytes, 0);
            var pn1 = ToUInt32(bytes, 4);
            var pn2 = ToUInt32(bytes, 8);
            var pn3 = ToUInt32(bytes, 12);
            var pn4 = ToUInt32(bytes, 16);

            return new Address(pn0, pn1, pn2, pn3, pn4, bytes, str);
        }

        private static uint ToUInt32(byte[] value, int index)
        {
            return value[index]
                   + ((uint)value[index + 1] << 8)
                   + ((uint)value[index + 2] << 16)
                   + ((uint)value[index + 3] << 24);
        }

        public override string ToString()
        {
            return this.addressString;
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
            bool equals = true;
            equals &= this.pn0 == obj.pn0;
            equals &= this.pn1 == obj.pn1;
            equals &= this.pn2 == obj.pn2;
            equals &= this.pn3 == obj.pn3;
            equals &= this.pn4 == obj.pn4;
            return equals;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.pn0, this.pn1, this.pn2, this.pn3, this.pn4);
        }
    }
}