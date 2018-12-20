using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Represents an address used when sending or receiving funds.
    /// </summary>
    public struct Address
    {
        public static Address Zero = new Address();
        public const int Width = 160 / 8;

        private readonly uint pn0;
        private readonly uint pn1;
        private readonly uint pn2;
        private readonly uint pn3;
        private readonly uint pn4;

        public Address(Address other)
        {
            this.pn0 = other.pn0;
            this.pn1 = other.pn1;
            this.pn2 = other.pn2;
            this.pn3 = other.pn3;
            this.pn4 = other.pn4;
        }

        /// <summary>
        /// Creates a new 160-bit wide Address.
        /// </summary>
        /// <param name="pn0">The first 32 bits of the address.</param>
        /// <param name="pn1">The second 32 bits of the address.</param>
        /// <param name="pn2">The third 32 bits of the address.</param>
        /// <param name="pn3">The fourth 32 bits of the address.</param>
        /// <param name="pn4">The last 32 bits of the address.</param>
        public Address(uint pn0, uint pn1, uint pn2, uint pn3, uint pn4)
        {
            this.pn0 = pn0;
            this.pn1 = pn1;
            this.pn2 = pn2;
            this.pn3 = pn3;
            this.pn4 = pn4;
        }

        public byte[] ToBytes()
        {
            const int uintSize = sizeof(uint);
            var arr = new byte[Width];
            Buffer.BlockCopy(BitConverter.GetBytes(this.pn0), 0, arr, uintSize * 0, uintSize);
            Buffer.BlockCopy(BitConverter.GetBytes(this.pn1), 0, arr, uintSize * 1, uintSize);
            Buffer.BlockCopy(BitConverter.GetBytes(this.pn2), 0, arr, uintSize * 2, uintSize);
            Buffer.BlockCopy(BitConverter.GetBytes(this.pn3), 0, arr, uintSize * 3, uintSize);
            Buffer.BlockCopy(BitConverter.GetBytes(this.pn4), 0, arr, uintSize * 4, uintSize);

            return arr;
        }

        public override string ToString()
        {
            return string.Concat(
                UIntToHexString(this.pn0),
                UIntToHexString(this.pn1),
                UIntToHexString(this.pn2),
                UIntToHexString(this.pn3),
                UIntToHexString(this.pn4)
            );
        }

        private static string UIntToHexString(uint val)
        {
            const string alphabet = "0123456789ABCDEF";

            return string.Concat(
                alphabet[(int) ((val & 0x000000F0) >> 1 * 4)],
                alphabet[(int) (val & 0x0000000F)],
                alphabet[(int) ((val & 0x0000F000) >> 3 * 4)],
                alphabet[(int) ((val & 0x00000F00) >> 2 * 4)],
                alphabet[(int) ((val & 0x00F00000) >> 5 * 4)],
                alphabet[(int) ((val & 0x000F0000) >> 4 * 4)],
                alphabet[(int) ((val & 0xF0000000) >> 7 * 4)],
                alphabet[(int) ((val & 0x0F000000) >> 6 * 4)]);
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