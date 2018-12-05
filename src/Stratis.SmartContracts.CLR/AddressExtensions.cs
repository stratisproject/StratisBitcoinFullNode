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
            return Address.Create(address.ToBytes());
        }

        public static Address ToAddress(this string address, Network network)
        {
            return Address.Create(address.ToUint160(network).ToBytes());
        }

        public static Address ToAddress(this byte[] bytes)
        {
            if (bytes.Length != Address.Width)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Address must be 20 bytes wide");

            return Address.Create(bytes);
        }

        public static Address HexToAddress(this string hexString)
        {
            // uint160 only parses a big-endian hex string
            var result = Extensions.HexStringToBytes(hexString);
            return Address.Create(result);
        }

        public static string ToBase58Address(this uint160 address, Network network)
        {
            return new BitcoinPubKeyAddress(new KeyId(address), network).ToString();
        }
    }
}