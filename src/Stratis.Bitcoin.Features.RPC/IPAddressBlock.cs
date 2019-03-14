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
        /// <summary>The CIDR suffix determines the number of significant bits in the <see cref="BlockAddress"/>.</summary>
        public int CIDRSuffix { get; private set; }

        /// <summary>The block address which is the same as the first ip address in the block.</summary>
        public IPAddress BlockAddress { get; private set; }

        /// <summary>The maximum length of the CIDR suffix.</summary>
        public int CIDRMaxSuffix => 8 * this.BlockAddress.GetAddressBytes().Length;

        /// <summary>
        /// Constructs an object representing a block of ip addresses.
        /// </summary>
        /// <param name="blockAddress">The block address.</param>
        /// <param name="suffix">The suffix determines the number of significant bits of the block address.</param>
        /// <exception cref="FormatException">If the block format is incorrect.</exception>
        public IPAddressBlock(IPAddress blockAddress, int? suffix = null)
        {
            // Normalize address.
            if (blockAddress.IsIPv4MappedToIPv6)
            {
                blockAddress = blockAddress.MapToIPv4();
                if (suffix != null)
                    suffix -= 96;
            }

            this.BlockAddress = blockAddress;

            if (suffix != null && (suffix < 0 || suffix > this.CIDRMaxSuffix))
                throw new FormatException("Invalid IPAddressBlock format");

            this.CIDRSuffix = suffix ?? this.CIDRMaxSuffix;
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
        /// <remarks>
        /// As per https://www.mediawiki.org/wiki/Help:Range_blocks - "Technical Explanation":
        ///
        /// CIDR notation is written as the IP address, a slash, and the CIDR suffix (for example, the
        /// IPv4 "10.2.3.41/24" or IPv6 "a3:bc00::/24").  The CIDR suffix is the number of starting digits
        /// every IP address in the range have in common when written in binary.
        ///
        /// For example: "10.10.1.32" is binary "00001010.00001010.00000001.00100000",  so 10.10.1.32/27
        /// will match the first 27 digits("00001010.00001010.00000001.00100000"). The IP addresses
        /// 10.10.1.32–10.10.1.63, when converted to binary, all have the same 27 first digits and will be blocked
        /// if 10.10.1.32 / 27 is blocked.
        /// </remarks>
        public bool Contains(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (this.BlockAddress.AddressFamily != address.AddressFamily)
                return false;

            byte[] blockAddrBytes = this.BlockAddress.GetAddressBytes();
            if (this.CIDRSuffix == (blockAddrBytes.Length * 8))
                return this.BlockAddress.Equals(address);

            // Determine the number of initial bytes in the block address that should match the ip address.
            int maskBytesLength = this.CIDRSuffix / 8;
            byte[] addressBytes = address.GetAddressBytes();
            for (int i = 0; i < maskBytesLength; i++)
                if (addressBytes[i] != blockAddrBytes[i])
                    return false;

            // Deal with CIDR suffixes that are mot a multiple of 8.
            if ((this.CIDRSuffix % 8) != 0)
            {
                // Determines the number of remaining bits of the CIDR suffix to compare.
                // Computes a byte mask as follows: 1 -> 0x80, 2 -> 0xC0, 3 -> 0xE0 .. 7 -> 0xFE.
                byte byteMask = (byte)~(0xff >> (this.CIDRSuffix % 8));

                if (((addressBytes[maskBytesLength] ^ blockAddrBytes[maskBytesLength]) & byteMask) != 0)
                    return false;
            }

            // We don't care about mismatches in the rest of the bytes.
            return true;
        }

        /// <summary>
        /// Returns the block of ip addresses defined by this object as a string.
        /// </summary>
        /// <returns>The block of ip addresses defined by this object as a string.</returns>
        public override string ToString()
        {
            if (this.CIDRSuffix == this.CIDRMaxSuffix)
                return this.BlockAddress.ToString();

            return $"{ this.BlockAddress }/{ this.CIDRSuffix }";
        }
    }
}
