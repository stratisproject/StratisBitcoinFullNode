using NBitcoin;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// This static class is used inside smart contracts. Developers can access each field.
    /// This may be non-static and injected via constructor in the future.
    /// </summary>
    public static class Block
    {
        /// <summary>
        /// The number of the current block in the Stratis blockchain.
        /// </summary>
        public static ulong Number { get; private set; }

        /// <summary>
        /// The coinbase account of the miner that constructed the current block.
        /// </summary>
        public static uint160 Coinbase { get; private set; }

        /// <summary>
        /// The current proof-of-work difficulty level.
        /// </summary>
        public static ulong Difficulty { get; private set; }

        internal static void Set(ulong number, uint160 coinbase, ulong difficulty)
        {
            Number = number;
            Coinbase = coinbase;
            Difficulty = difficulty;
        }
    }
}
