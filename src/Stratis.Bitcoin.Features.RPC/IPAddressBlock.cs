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
        public bool IsFullMask => this.Mask == this.FullMask;

        /// <summary>
        /// Constructs an object representing a block of ip addresses.
        /// </summary>
        /// <param name="address">The base ip address.</param>
        /// <param name="mask">The mask identifies the number of significant bits of the base ip address.</param>
        public IPAddressBlock(IPAddress address, int? mask = null)
        {
            this.Address = address;
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
            byte[] addressV6 = address.EnsureIPv6().GetAddressBytes();
            byte[] blockAddr = this.Address.EnsureIPv6().GetAddressBytes();

            int bitsToKeep = (this.FullMask < blockAddr.Length * 8) ? this.Mask + 96 : this.Mask;
            int bytesToKeep = bitsToKeep / 8;

            if ((bitsToKeep % 8) != 0)
                addressV6[bytesToKeep++] &= (byte)(0xff00 >> (bitsToKeep % 8));

            while (bytesToKeep < addressV6.Length)
                addressV6[bytesToKeep++] = 0;

            return Utils.ArrayEqual(addressV6, blockAddr);
        }

        /// <summary>
        /// Returns the block of ip address defined by this object as a string.
        /// </summary>
        /// <returns>The block of ip address defined by this object as a string.</returns>
        public override string ToString()
        {
            if (this.IsFullMask)
                return this.Address.ToString();

            return $"{ this.Address }/{ this.Mask }";
        }
    }
}
