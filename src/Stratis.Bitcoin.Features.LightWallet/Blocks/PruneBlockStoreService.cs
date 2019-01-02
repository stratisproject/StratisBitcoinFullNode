using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.LightWallet.Blocks
{
    /// <inheritdoc/>
    public sealed class PruneBlockStoreService : IPruneBlockStoreService
    {
        private IAsyncLoop asyncLoop;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly IBlockRepository blockRepository;
        private readonly IChainState chainState;
        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;
        private readonly IPrunedBlockRepository prunedBlockRepository;
        private readonly StoreSettings storeSettings;

        /// <inheritdoc/>
        public ChainedHeader PrunedUpToHeaderTip { get; private set; }

        public PruneBlockStoreService(
            IAsyncLoopFactory asyncLoopFactory,
            IBlockRepository blockRepository,
            IPrunedBlockRepository prunedBlockRepository,
            IChainState chainState,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime,
            StoreSettings storeSettings)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockRepository = blockRepository;
            this.prunedBlockRepository = prunedBlockRepository;
            this.chainState = chainState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.storeSettings = storeSettings;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            this.PrunedUpToHeaderTip = this.chainState.BlockStoreTip.GetAncestor(this.prunedBlockRepository.PrunedTip.Height);

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.PruneBlocksAsync)}", async token =>
            {
                await this.PruneBlocksAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.TenSeconds);
        }

        /// <summary>
        /// Delete blocks continuously from the back of the store.
        /// </summary>
        private async Task PruneBlocksAsync()
        {
            if (this.blockRepository.TipHashAndHeight.Height < this.storeSettings.AmountOfBlocksToKeep)
            {
                this.logger.LogTrace("(-)[BLOCKSTORE_HEIGHT_BELOW_AMOUNTOFBLOCKSTOKEEP]");
                return;
            }

            if (this.blockRepository.TipHashAndHeight.Height == (this.PrunedUpToHeaderTip?.Height ?? 0))
            {
                this.logger.LogTrace("(-)[BLOCKSTORE_HEIGHT_EQUALS_PRUNEDTIP]");
                return;
            }

            if (this.blockRepository.TipHashAndHeight.Height < (this.PrunedUpToHeaderTip?.Height ?? 0 + this.storeSettings.AmountOfBlocksToKeep))
            {
                this.logger.LogTrace("(-)[BLOCKSTORE_HEIGHT_BELOW_PRUNEDTIP_PLUS_AMOUNTTOKEEP]");
                return;
            }

            int heightToPruneFrom = this.blockRepository.TipHashAndHeight.Height - this.storeSettings.AmountOfBlocksToKeep;
            ChainedHeader startFrom = this.chainState.BlockStoreTip.GetAncestor(heightToPruneFrom);
            if (startFrom == null)
            {
                this.logger.LogInformation("Prune aborted, start block at height {0} was not found.", heightToPruneFrom);
                return;
            }

            if (this.PrunedUpToHeaderTip != null && startFrom == this.PrunedUpToHeaderTip)
            {
                this.logger.LogInformation("Prune aborted, start block at height {0} equals the pruned tip.", heightToPruneFrom);
                return;
            }

            this.logger.LogInformation("Pruning triggered, delete from {0} to {1}.", heightToPruneFrom, this.PrunedUpToHeaderTip?.Height ?? 0);

            var chainedHeadersToDelete = new List<ChainedHeader>();
            while (startFrom.Previous != null && this.PrunedUpToHeaderTip != startFrom)
            {
                chainedHeadersToDelete.Add(startFrom);
                startFrom = startFrom.Previous;
            }

            this.logger.LogDebug($"{chainedHeadersToDelete.Count} blocks will be pruned.");

            ChainedHeader prunedTip = chainedHeadersToDelete.First();

            await this.blockRepository.DeleteBlocksAsync(chainedHeadersToDelete.Select(c => c.HashBlock).ToList());
            this.prunedBlockRepository.UpdatePrunedTip(prunedTip);

            this.PrunedUpToHeaderTip = prunedTip;

            this.logger.LogInformation($"Store has been pruned up to {this.PrunedUpToHeaderTip.Height}.");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
        }
    }
}
