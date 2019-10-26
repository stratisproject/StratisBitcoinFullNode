namespace NBitcoin
{
    public class PrecomputedTransactionData
    {
        public PrecomputedTransactionData(Transaction tx)
        {
            this.HashOutputs = Script.GetHashOutputs(tx);
            this.HashSequence = Script.GetHashSequence(tx);
            this.HashPrevouts = Script.GetHashPrevouts(tx);
        }
        public uint256 HashPrevouts
        {
            get;
            set;
        }
        public uint256 HashSequence
        {
            get;
            set;
        }
        public uint256 HashOutputs
        {
            get;
            set;
        }
    }
}
