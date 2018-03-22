namespace Stratis.SmartContracts
{
    public struct Block
    {
        public Block(ulong number, Address coinbase, ulong difficulty)
        {
            this.Number = number;
            this.Coinbase = coinbase;
            this.Difficulty = difficulty;
        }

        public Address Coinbase { get; }
        public ulong Difficulty { get; }
        public ulong Number { get; }
    }
}