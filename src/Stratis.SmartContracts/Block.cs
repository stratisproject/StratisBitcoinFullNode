using NBitcoin;

namespace Stratis.SmartContracts
{
    public static class Block
    {
        public static ulong Number { get; private set; }
        public static uint160 Coinbase { get; private set; }
        public static ulong Difficulty { get; private set; }
        internal static void Set(ulong number, uint160 coinbase, ulong difficulty)
        {
            Number = number;
            Coinbase = coinbase;
            Difficulty = difficulty;
        }
    }
}
