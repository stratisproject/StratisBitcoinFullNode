using System.Net;
using NBitcoin;

namespace Stratis.Bitcoin.Features.RPC
{
    public class IPAddressBlock
    {
        public int mask;
        public IPAddress address;

        public int FullMask => (int)(8 * this.address.GetAddressBytes().Length);
        public bool IsFullMask => this.mask == this.FullMask;

        public IPAddressBlock(IPAddress address, int? mask = null)
        {
            this.address = address;
            this.mask = mask ?? this.FullMask;
        }

        public static IPAddressBlock Parse(string value)
        {
            string[] parts = value.Split('/');
            return new IPAddressBlock(IPAddress.Parse(parts[0]), (parts.Length < 2) ? (int?)null : int.Parse(parts[1]));
        }

        public bool Contains(IPAddress address)
        {
            byte[] addressV6 = address.EnsureIPv6().GetAddressBytes();
            byte[] blockAddr = this.address.EnsureIPv6().GetAddressBytes();

            int bitsToKeep = (this.FullMask < blockAddr.Length * 8) ? this.mask + 96 : this.mask;
            if (bitsToKeep == 0)
                return true;

            int bytesToKeep = bitsToKeep / 8;
            if ((bitsToKeep % 8) != 0)
                addressV6[bytesToKeep++] &= (byte)(256 >> (bitsToKeep % 8));

            for (int i = bytesToKeep; i < 16; i++)
                addressV6[i] = 0;

            return Utils.ArrayEqual(addressV6, blockAddr);
        }
    }
}
