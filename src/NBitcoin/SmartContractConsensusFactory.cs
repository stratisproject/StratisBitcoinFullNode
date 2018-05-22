namespace NBitcoin
{
    public class SmartContractConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractBlockHeader();
        }

        /// <inheritdoc />
        public override bool TryCreateNew<T>(out T result)
        {
            result = default(T);
            if (this.IsBlock<T>())
            {
                result = (T)(object)this.CreateBlock();
                return true;
            }
            if (this.IsBlockHeader<T>())
            {
                result = (T)(object)this.CreateBlockHeader();
                return true;
            }
            if (this.IsTransaction<T>())
            {
                result = (T)(object)this.CreateTransaction();
                return true;
            }

            return false;
        }
    }
}