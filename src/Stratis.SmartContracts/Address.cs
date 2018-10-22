using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Represents an address used when sending or receiving funds.
    /// </summary>
    public struct Address
    {
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
        }

        private Address(uint pn0, uint pn1, uint pn2, uint pn3, uint pn4, byte[] bytes)
        {
            this.pn0 = pn0;
            this.pn1 = pn1;
            this.pn2 = pn2;
            this.pn3 = pn3;
            this.pn4 = pn4;
            this.Bytes = bytes;
        }
        
        internal static Address Create(byte[] bytes)
        {
            var pn0 = ToUInt32(bytes, 0);
            var pn1 = ToUInt32(bytes, 4);
            var pn2 = ToUInt32(bytes, 8);
            var pn3 = ToUInt32(bytes, 12);
            var pn4 = ToUInt32(bytes, 16);

            return new Address(pn0, pn1, pn2, pn3, pn4, bytes);
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
            string result = "";

            result += UIntToHexString(this.pn0);
            result += UIntToHexString(this.pn1);
            result += UIntToHexString(this.pn2);
            result += UIntToHexString(this.pn3);
            result += UIntToHexString(this.pn4);

            return result;
        }

        private static string UIntToHexString(uint val)
        {
            string result = "";
            const string alphabet = "0123456789ABCDEF";

            result += alphabet[(int)((val & 0x000000F0) >> 1 * 4)];
            result += alphabet[(int)(val & 0x0000000F)];

            result += alphabet[(int)((val & 0x0000F000) >> 3 * 4)];
            result += alphabet[(int)((val & 0x00000F00) >> 2 * 4)];

            result += alphabet[(int)((val & 0x00F00000) >> 5 * 4)];
            result += alphabet[(int)((val & 0x000F0000) >> 4 * 4)];

            result += alphabet[(int)((val & 0xF0000000) >> 7*4)];
            result += alphabet[(int)((val & 0x0F000000) >> 6*4)];

            return result;
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
    }
}