using NBitcoin.Protocol;
using NBitcoin;

namespace LedgerWallet
{
    class VarintUtils
    {
        internal static void write(System.IO.MemoryStream data, int p)
        {
            var b = new VarInt((ulong)p).ToBytes();
            data.Write(b, 0, b.Length);
        }
    }
}
