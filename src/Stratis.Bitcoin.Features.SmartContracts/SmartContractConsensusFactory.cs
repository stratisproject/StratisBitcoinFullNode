using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractBlockHeader();
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