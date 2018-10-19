using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
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

        public static uint160 ToUint160(this Address address, Network network)
        {
            return new uint160(address.Bytes);
        }

        public static uint160 ToUint160(this string addressString, Network network)
        {
            return new uint160(new BitcoinPubKeyAddress(addressString, network).Hash.ToBytes());
        }

        public static Address ToAddress(this uint160 address, Network network)
        {
            return Address.Create(address.ToBytes(), Uint160ToAddressString(address, network));
        }

        public static Address ToAddress(this string address, Network network)
        {
            return Address.Create(address.ToUint160(network).ToBytes(), address);
        }

        public static Address HexToAddress(this string hexString, Network network)
        {
            return ToAddress(new uint160(hexString), network);
        }

        public static string BytesToAddressString(this byte[] bytes, Network network)
        {
            return new BitcoinPubKeyAddress(new KeyId(new uint160(bytes)), network).ToString();
        }

        private static string Uint160ToAddressString(uint160 address, Network network)
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