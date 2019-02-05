using System;
using System.Net;
using NBitcoin;

namespace Stratis.Bitcoin.Features.RPC
{
    /// <summary>
    /// The class of an object representing a block of ip addresses.
    /// </summary>
    public class IPAddressBlock
    {
        public int Mask { get; private set; }
        public IPAddress Address { get; private set; }

        public int FullMask => 8 * this.Address.GetAddressBytes().Length;

        /// <summary>
        /// Constructs an object representing a block of ip addresses.
        /// </summary>
        /// <param name="address">The base ip address.</param>
        /// <param name="mask">The mask identifies the number of significant bits of the base ip address.</param>
        /// <exception cref="FormatException">If the block format is incorrect.</exception>
        public IPAddressBlock(IPAddress address, int? mask = null)
        {
            // Normalize address.
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
                if (mask != null)
                    mask -= 96;
            }

            this.Address = address;

            if (mask != null && (mask < 0 || mask > this.FullMask))
                throw new FormatException("Invalid IPAddressBlock format");

            this.Mask = mask ?? this.FullMask;
        }

        /// <summary>
        /// Parses the string representation of a block of ip addresses and creates a corresponding object.
        /// </summary>
        /// <param name="value">The string representation of a block of ip addresses.</param>
        /// <returns>An object representing a block of ip addresses.</returns>
        public static IPAddressBlock Parse(string value)
        {
            string[] parts = value.Split('/');
            return new IPAddressBlock(IPAddress.Parse(parts[0]), (parts.Length < 2) ? (int?)null : int.Parse(parts[1]));
        }

        /// <summary>
        /// Determines if the passed ip address occurs in this range.
        /// </summary>
        /// <param name="address">The ip adddress.</param>
        /// <returns><c>True</c> if the ip address occurs in this range and <c>false</c> otherwise.</returns>
        public bool Contains(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (this.Address.AddressFamily != address.AddressFamily)
                return false;

            byte[] blockAddrBytes = this.Address.GetAddressBytes();
            if ((this.Mask == (blockAddrBytes.Length * 8)) && (this.Address == address))
                return true;

            byte[] addressBytes = address.GetAddressBytes();
            int bytesToKeep = this.Mask / 8;
            if ((this.Mask % 8) != 0)
                addressBytes[bytesToKeep++] &= (byte)~(0xff >> (this.Mask % 8));

            while (bytesToKeep < addressBytes.Length)
                addressBytes[bytesToKeep++] = 0;

            return Utils.ArrayEqual(addressBytes, blockAddrBytes);
        }

        /// <summary>
        /// Returns the block of ip address defined by this object as a string.
        /// </summary>
        /// <returns>The block of ip address defined by this object as a string.</returns>
        public override string ToString()
        {
            if (this.Mask == this.FullMask)
                return this.Address.ToString();

            return $"{ this.Address }/{ this.Mask }";
        }
    }
}
