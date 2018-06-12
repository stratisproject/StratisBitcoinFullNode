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
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static uint160 ToUint160(this Address address, Network network)
        {
            return new uint160(new BitcoinPubKeyAddress(address.Value, network).Hash.ToBytes());
        }

        public static Address ToAddress(this uint160 address, Network network)
        {
            return new Address(new BitcoinPubKeyAddress(new KeyId(address), network).ToString());
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