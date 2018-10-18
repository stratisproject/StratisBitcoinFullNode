using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Helper struct that represents a STRAT address and is used when sending or receiving funds.
    /// <para>
    /// Note that the format of the address is not validated on construction, but when trying to send funds to this address.
    /// </para>
    /// </summary>
    public class Address
    {
        /// <summary>
        /// The address as a string, in base58 format.
        /// </summary>
        public readonly string Value;

        private Network network;
        public byte[] Bytes { get; }

        private const int AddressWidth = 160 / 8;
        internal readonly uint pn0;
        internal readonly uint pn1;
        internal readonly uint pn2;
        internal readonly uint pn3;
        internal readonly uint pn4;

        /// <summary>
        /// Create a new address
        /// </summary>
        public Address(string address)
        {
            this.network = null;
            this.Bytes = new byte[0];
            this.Value = address;
        }

        private Address(uint pn0, uint pn1, uint pn2, uint pn3, uint pn4, Network network)
        {
            this.pn0 = pn0;
            this.pn1 = pn1;
            this.pn2 = pn2;
            this.pn3 = pn3;
            this.pn4 = pn4;
            this.network = network;
            this.Bytes = this.ToBytes();
            this.Value = "";
        }

        private byte[] ToBytes()
        {
            var arr = new byte[AddressWidth];
            Buffer.BlockCopy(Utils.ToBytes(this.pn0, true), 0, arr, 4 * 0, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn1, true), 0, arr, 4 * 1, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn2, true), 0, arr, 4 * 2, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn3, true), 0, arr, 4 * 3, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn4, true), 0, arr, 4 * 4, 4);
            return arr;
        }

        internal static Address Create(byte[] bytes, Network network)
        {
            // Default to empty bytes
            if (bytes == null)
                return new Address(0, 0, 0, 0, 0, network);

            var pn0 = ToUInt32(bytes, 0);
            var pn1 = ToUInt32(bytes, 4);
            var pn2 = ToUInt32(bytes, 8);
            var pn3 = ToUInt32(bytes, 12);
            var pn4 = ToUInt32(bytes, 16);

            return new Address(pn0, pn1, pn2, pn3, pn4, network);
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
            byte[] versionBytes = this.network.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true);
            return Encoders.Base58Check.EncodeData(versionBytes.Concat(this.Bytes).ToArray());
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