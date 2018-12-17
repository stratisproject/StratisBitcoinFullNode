using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.LightWallet.Blocks
{
    /// <inheritdoc/>
    public sealed class LightWalletBlockStoreService : ILightWalletBlockStoreService
    {
        private IAsyncLoop asyncLoop;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly IBlockRepository blockRepository;
        private readonly IConsensusManager consensusManager;
        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;
        private readonly StoreSettings storeSettings;

        /// <inheritdoc/>
        public ChainedHeader PrunedUpToHeaderTip { get; private set; }

        public LightWalletBlockStoreService(
            IAsyncLoopFactory asyncLoopFactory,
            IBlockRepository blockRepository,
            IConsensusManager consensusManager,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime,
            StoreSettings storeSettings)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockRepository = blockRepository;
            this.consensusManager = consensusManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.storeSettings = storeSettings;
        }

        /// <inheritdoc/>
        public void Start()
        {
            this.PrunedUpToHeaderTip = this.consensusManager.Tip.GetAncestor(this.blockRepository.PrunedTip.Height);

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.PruneBlocksAsync)}", async token =>
            {
                await PruneBlocksAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.TenSeconds);
        }

        /// <summary>
        /// Delete blocks continuously from the back of the store.
        /// </summary>
        private async Task PruneBlocksAsync()
        {
            if (this.blockRepository.TipHashAndHeight.Height < this.storeSettings.PruneBlockMargin)
                return;

            if (this.blockRepository.TipHashAndHeight.Height == (this.PrunedUpToHeaderTip?.Height ?? 0))
                return;

            if (this.blockRepository.TipHashAndHeight.Height < (this.PrunedUpToHeaderTip?.Height ?? 0 + this.storeSettings.PruneBlockMargin))
                return;

            var heightToPruneFrom = this.blockRepository.TipHashAndHeight.Height - this.storeSettings.PruneBlockMargin;
            ChainedHeader startFrom = this.consensusManager.Tip.GetAncestor(heightToPruneFrom);
            if (this.PrunedUpToHeaderTip != null && startFrom == this.PrunedUpToHeaderTip)
                return;

            this.logger.LogInformation($"Pruning triggered, delete from {heightToPruneFrom} to {this.PrunedUpToHeaderTip?.Height ?? 0}.");

            var chainedHeadersToDelete = new List<ChainedHeader>();
            while (startFrom.Previous != null && this.PrunedUpToHeaderTip != startFrom)
            {
                chainedHeadersToDelete.Add(startFrom);
                startFrom = startFrom.Previous;
            }

            this.logger.LogDebug($"{chainedHeadersToDelete.Count} blocks will be pruned.");

            ChainedHeader prunedTip = chainedHeadersToDelete.First();

            await this.blockRepository.DeleteBlocksAsync(chainedHeadersToDelete.Select(c => c.HashBlock).ToList());
            this.blockRepository.UpdatePrunedTip(prunedTip);

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
