using NBitcoin;

namespace Stratis.SmartContracts
{
    public static class Block
    {
        public static ulong Number { get; private set; }
        public static uint256 BlockHash { get; private set; }
        public static uint160 Coinbase { get; private set; }
        public static ulong Difficulty { get; private set; }
        internal static void Set(ulong number, uint256 blockHash, uint160 coinbase, ulong difficulty)
        {
            Number = number;
            BlockHash = BlockHash;
            Coinbase = coinbase;
            Difficulty = difficulty;
        }
    }
}
