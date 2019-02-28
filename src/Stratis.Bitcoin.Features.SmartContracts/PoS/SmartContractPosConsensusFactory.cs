using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS
{
    public class SmartContractPosConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractPosBlockHeader();
        }

        /// <inheritdoc />
        public override Block CreateBlock()
        {
            return new SmartContractPosBlock((SmartContractPosBlockHeader)this.CreateBlockHeader());
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction()
        {
            return new SmartContractPosTransaction();
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(byte[] bytes)
        {
            return new SmartContractPosTransaction(bytes);
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(string hex)
        {
            return new SmartContractPosTransaction(hex);
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