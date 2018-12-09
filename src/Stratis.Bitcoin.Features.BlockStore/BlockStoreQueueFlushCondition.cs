using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <inheritdoc/>
    public sealed class BlockStoreQueueFlushCondition : IBlockStoreQueueFlushCondition
    {
        private readonly IChainState chainState;

        public BlockStoreQueueFlushCondition(IChainState chainState)
        {
            this.chainState = chainState;
        }

        /// <inheritdoc/>
        public bool ShouldFlush
        {
            get
            {
                // Once store is 5 blocks form the consensus tip then flush on every block.
                if (this.chainState.ConsensusTip.Height - this.chainState.BlockStoreTip.Height < 5)
                    return true;

                return false;
            }
        }
    }
}
