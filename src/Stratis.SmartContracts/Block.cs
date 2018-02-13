using NBitcoin;

namespace Stratis.SmartContracts
{
    public class Block
    {
        public ulong Number { get; }
        public uint160 Coinbase { get; }
        public ulong Difficulty { get; }

        public Block(ulong number, uint160 coinbase, ulong difficulty)
        {
            Number = number;
            Coinbase = coinbase;
            Difficulty = difficulty;
        }
    }
}
