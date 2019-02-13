using System;
using System.Net;

namespace Stratis.Bitcoin.Features.RPC
{
    /// <summary>
    /// The class of an object representing a block of ip addresses.
    /// See https://www.mediawiki.org/wiki/Help:Range_blocks.
    /// </summary>
    public class IPAddressBlock
    {
        /// <summary>The mask length identifies the number of significant bits in the <see cref="BlockAddress"/>.</summary>
        public int MaskLength { get; private set; }

        /// <summary>The block address which is the same as the first ip address in the block.</summary>
        public IPAddress BlockAddress { get; private set; }

        /// <summary>The maximum length of the mask.</summary>
        public int FullMaskLengthInBits => 8 * this.BlockAddress.GetAddressBytes().Length;

        /// <summary>
        /// Constructs an object representing a block of ip addresses.
        /// </summary>
        /// <param name="blockAddress">The block address.</param>
        /// <param name="maskLength">The mask identifies the number of significant bits of the block address.</param>
        /// <exception cref="FormatException">If the block format is incorrect.</exception>
        public IPAddressBlock(IPAddress blockAddress, int? maskLength = null)
        {
            // Normalize address.
            if (blockAddress.IsIPv4MappedToIPv6)
            {
                blockAddress = blockAddress.MapToIPv4();
                if (maskLength != null)
                    maskLength -= 96;
            }

            this.BlockAddress = blockAddress;

            if (maskLength != null && (maskLength < 0 || maskLength > this.FullMaskLengthInBits))
                throw new FormatException("Invalid IPAddressBlock format");

            this.MaskLength = maskLength ?? this.FullMaskLengthInBits;
        }

        /// <summary>
        /// Allow implicit cast from IPAddress to IPAddressBlock for convenience.
        /// </summary>
        /// <param name="address">The IPAddress to cast to an IPAddressBlock.</param>
        public static implicit operator IPAddressBlock(IPAddress address)
        {
            return new IPAddressBlock(address);
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
        /// Determines if the passed ip address occurs in the CIDR range defined by this object.
        /// </summary>
        /// <param name="address">The ip adddress.</param>
        /// <returns><c>True</c> if the ip address occurs in this range and <c>false</c> otherwise.</returns>
        public bool Contains(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (this.BlockAddress.AddressFamily != address.AddressFamily)
                return false;

            byte[] blockAddrBytes = this.BlockAddress.GetAddressBytes();
            if (this.MaskLength == (blockAddrBytes.Length * 8))
                return this.BlockAddress.Equals(address);

            // Determine the number of initial bytes in the block address that should match the ip address.
            int maskBytesLength = this.MaskLength / 8;
            byte[] addressBytes = address.GetAddressBytes();
            for (int i = 0; i < maskBytesLength; i++)
                if (addressBytes[i] != blockAddrBytes[i])
                    return false;

            // Deal with fractional bytes.
            if ((this.MaskLength % 8) != 0)
                if (((addressBytes[maskBytesLength] ^ blockAddrBytes[maskBytesLength]) & (byte)~(0xff >> (this.MaskLength % 8))) != 0)
                    return false;

            // We don't care about mismatches in the rest of the bytes.
            return true;
        }

        /// <summary>
        /// Returns the block of ip addresses defined by this object as a string.
        /// </summary>
        /// <returns>The block of ip addresses defined by this object as a string.</returns>
        public override string ToString()
        {
            if (this.MaskLength == this.FullMaskLengthInBits)
                return this.BlockAddress.ToString();

            return $"{ this.BlockAddress }/{ this.MaskLength }";
        }
    }
}
