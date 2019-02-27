using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.PoW
{
    public sealed class SmartContractPowConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractPowBlockHeader();
        }

        /// <inheritdoc />
        public override T TryCreateNew<T>()
        {
            object result = null;

            if (this.IsBlock<T>())
                result = (T)(object)this.CreateBlock();

            if (this.IsBlockHeader<T>())
                result = (T)(object)this.CreateBlockHeader();

            if (this.IsTransaction<T>())
                result = (T)(object)this.CreateTransaction();

            return (T)result;
        }
    }
}