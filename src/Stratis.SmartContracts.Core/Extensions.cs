using System;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core
{
    public static class Extensions
    {
        public static string ToHexString(this byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        public static byte[] HexToByteArray(this string hex)
        {
            string toHex = hex;

            if (hex.StartsWith("0x"))
                toHex = hex.Substring(2);

            int NumberChars = toHex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(toHex.Substring(i, 2), 16);
            return bytes;
        }

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
            var result = HexStringToBytes(hexString);
            return Address.Create(result);
        }

        private static byte[] HexStringToBytes(string val)
        {
            if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                val = val.Substring(2);

            byte[] ret = new byte[val.Length / 2];
            for (int i = 0; i < val.Length; i = i + 2)
            {
                string hexChars = val.Substring(i, 2);
                ret[i / 2] = byte.Parse(hexChars, System.Globalization.NumberStyles.HexNumber);
            }
            return ret;
        }

        public static string ToBase58Address(this uint160 address, Network network)
        {
            return new BitcoinPubKeyAddress(new KeyId(address), network).ToString();
        }

        public static Money GetFee(this Transaction transaction, UnspentOutputSet inputs)
        {
            Money valueIn = Money.Zero;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                OutPoint prevout = transaction.Inputs[i].PrevOut;
                UnspentOutputs coins = inputs.AccessCoins(prevout.Hash);
                valueIn += coins.TryGetOutput(prevout.N).Value;
            }
            return valueIn - transaction.TotalOut;
        }
    }
}