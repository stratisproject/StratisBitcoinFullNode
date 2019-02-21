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

            int numberChars = toHex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(toHex.Substring(i, 2), 16);
            return bytes;
        }

        public static byte[] HexStringToBytes(string val)
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