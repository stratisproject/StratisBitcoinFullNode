using System;
using NBitcoin;
using Extensions = Stratis.SmartContracts.Core.Extensions;

namespace Stratis.SmartContracts.CLR
{
    public static class AddressExtensions
    {
        public static uint160 ToUint160(this Address address)
        {
            return new uint160(address.ToBytes());
        }

        public static uint160 ToUint160(this string base58Address, Network network)
        {
            return new uint160(new BitcoinPubKeyAddress(base58Address, network).Hash.ToBytes());
        }

        public static Address ToAddress(this uint160 address)
        {
            return CreateAddress(address.ToBytes());
        }

        public static Address ToAddress(this string address, Network network)
        {
            return CreateAddress(address.ToUint160(network).ToBytes());
        }

        public static Address ToAddress(this byte[] bytes)
        {
            if (bytes.Length != Address.Width)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Address must be 20 bytes wide");

            return CreateAddress(bytes);
        }

        public static Address HexToAddress(this string hexString)
        {
            // uint160 only parses a big-endian hex string
            byte[] result = Extensions.HexStringToBytes(hexString);
            return CreateAddress(result);
        }

        public static string ToBase58Address(this uint160 address, Network network)
        {
            return new BitcoinPubKeyAddress(new KeyId(address), network).ToString();
        }

        private static Address CreateAddress(byte[] bytes)
        {
            uint pn0 = BitConverter.ToUInt32(bytes, 0);
            uint pn1 = BitConverter.ToUInt32(bytes, 4);
            uint pn2 = BitConverter.ToUInt32(bytes, 8);
            uint pn3 = BitConverter.ToUInt32(bytes, 12);
            uint pn4 = BitConverter.ToUInt32(bytes, 16);

            return new Address(pn0, pn1, pn2, pn3, pn4);
        }
    }
}