using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <inheritdoc/>
    public sealed class BlockStoreQueueFlushCondition : IBlockStoreQueueFlushCondition
    {
        private readonly IChainState chainState;
        private readonly IInitialBlockDownloadState blockDownloadState;

        public BlockStoreQueueFlushCondition(IChainState chainState, IInitialBlockDownloadState blockDownloadState)
        {
            this.chainState = chainState;
            this.blockDownloadState = blockDownloadState;
        }

        /// <inheritdoc/>
        public bool ShouldFlush
        {
            get
            {
                // If the node is in IBD we don't flush on each block.
                if (this.blockDownloadState.IsInitialBlockDownload())
                    return false;

                int distanceFromConsensusTip = this.chainState.ConsensusTip.Height - this.chainState.BlockStoreTip.Height;

                // Once store is 5 blocks form the consensus tip then flush on every block.
                if (distanceFromConsensusTip < 5)
                    return true;

                return false;
            }
        }
    }
}
