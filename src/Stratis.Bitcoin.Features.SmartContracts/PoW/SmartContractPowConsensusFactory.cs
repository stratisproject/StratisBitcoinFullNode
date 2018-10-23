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

            if (IsBlock<T>())
                result = (T)(object)CreateBlock();

            if (IsBlockHeader<T>())
                result = (T)(object)CreateBlockHeader();

            if (IsTransaction<T>())
                result = (T)(object)CreateTransaction();

            return (T)result;
        }
    }
}