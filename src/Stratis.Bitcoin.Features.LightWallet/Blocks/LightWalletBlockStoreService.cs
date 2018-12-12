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
    public sealed class LightWalletBlockStoreService : ILightWalletBlockStoreService
    {
        private IAsyncLoop asyncLoop;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly IBlockRepository blockStore;
        private readonly IConsensusManager consensusManager;
        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;

        public ChainedHeader PrunedUpToHeader { get; private set; }

        private const int MaxBlocksToKeep = 500;

        public LightWalletBlockStoreService(
            IAsyncLoopFactory asyncLoopFactory,
            IBlockRepository blockStore,
            IConsensusManager consensusManager,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockStore = blockStore;
            this.consensusManager = consensusManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
        }

        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.PruneBlocksAsync)}", async token =>
            {
                await PruneBlocksAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.TenSeconds);
        }

        /// <summary>
        /// Deletes blocks continuously from the back of the store.
        /// </summary>
        private async Task PruneBlocksAsync()
        {
            if (this.blockStore.TipHashAndHeight.Height < MaxBlocksToKeep)
                return;

            if (this.blockStore.TipHashAndHeight.Height == (this.PrunedUpToHeader?.Height ?? 0))
                return;

            // 1500 - 1000
            // Pruned 1400 to 1000
            if (this.blockStore.TipHashAndHeight.Height < (this.PrunedUpToHeader?.Height ?? 0 + MaxBlocksToKeep))
                return;

            var heightToPruneFrom = this.blockStore.TipHashAndHeight.Height - MaxBlocksToKeep;
            ChainedHeader startFrom = this.consensusManager.Tip.GetAncestor(heightToPruneFrom);
            if (this.PrunedUpToHeader != null && startFrom == this.PrunedUpToHeader)
                return;

            this.logger.LogDebug($"Pruning triggered, delete from {heightToPruneFrom} to {this.PrunedUpToHeader?.Height ?? 0}.");

            var chainedHeadersToDelete = new List<ChainedHeader>();

            while (startFrom.Previous != null)
            {
                chainedHeadersToDelete.Add(startFrom);
                startFrom = startFrom.Previous;
            }

            this.logger.LogDebug($"{chainedHeadersToDelete.Count} blocks will be pruned.");

            await this.blockStore.DeleteBlocksAsync(chainedHeadersToDelete.Select(ch => ch.HashBlock).ToList());

            this.PrunedUpToHeader = chainedHeadersToDelete.First();

            this.logger.LogDebug($"Store has been pruned to {this.PrunedUpToHeader}");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
        }
    }
}
