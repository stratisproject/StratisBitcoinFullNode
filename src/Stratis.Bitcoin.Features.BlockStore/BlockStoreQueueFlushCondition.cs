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
        public bool ShouldFlush { get { return this.chainState.IsAtBestChainTip; } }
    }
}
