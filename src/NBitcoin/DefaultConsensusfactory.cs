using System;

namespace NBitcoin
{
    /// <summary>
    /// A default object factory to create instances that is not block, block header or transaction.
    /// </summary>
    public sealed class DefaultConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc/>
        public override T TryCreateNew<T>()
        {
            if (this.IsBlock<T>() || this.IsBlockHeader<T>() || this.IsTransaction<T>())
                throw new Exception(string.Format("{0} cannot be created by this consensus factory, please use the appropriate one.", typeof(T).Name));

            return default(T);
        }
    }
}