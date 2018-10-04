using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAMiner : IDisposable
    {
        // TODO POA. First add creation of block template and later apply signatures and other stuff. We need bare minimum to test syncing and payloads

        private readonly IConsensusManager consensusManager;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ILogger logger;

        private readonly Network network;

        /// <summary>
        /// A cancellation token source that can cancel the mining processes and is linked to the <see cref="INodeLifetime.ApplicationStopping"/>.
        /// </summary>
        private CancellationTokenSource cancellation;

        private readonly IInitialBlockDownloadState indState;

        private readonly PoABlockDefinition blockDefinition;

        private Task miningTask;

        public PoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState indState,
            PoABlockDefinition blockDefinition)
        {
            this.consensusManager = consensusManager;
            this.dateTimeProvider = dateTimeProvider;
            this.network = network;
            this.indState = indState;
            this.blockDefinition = blockDefinition;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(new[] { nodeLifetime.ApplicationStopping });
        }

        /// <summary>Starts mining loop.</summary>
        public void InitializeMining()
        {
            if (this.miningTask == null)
            {
                this.miningTask = this.CreateBlocksAsync();
            }
        }

        private async Task CreateBlocksAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                // TODO try catch and check for cancellation

                // Don't mine in IBD.
                if (this.indState.IsInitialBlockDownload())
                {
                    int attemptDelayMs = 20_000;
                    await Task.Delay(attemptDelayMs, this.cancellation.Token).ConfigureAwait(false);

                    continue;
                }

                // TODO check that on fresh start we are not in IBD

                // TODO POA check if our timestamp and if not wait for our timestamp


                // TODO poa custom block provider that will set nonce and target to const
                ChainedHeader tip = this.consensusManager.Tip;

                //BlockTemplate blockTemplate = this.blockProvider.BuildPowBlock(tip); // TODO
                BlockTemplate blockTemplate = null; // TODO remove

                //if (this.network.Consensus.IsProofOfStake) //TODO POA make sure we always mine with timestamp greater than prev block
                //{
                //    if (context.BlockTemplate.Block.Header.Time <= context.ChainTip.Header.Time)
                //        return false;
                //}

                // TODO sign the block with out signature

                ChainedHeader chainedHeader = await this.consensusManager.BlockMinedAsync(blockTemplate.Block).ConfigureAwait(false);

                if (chainedHeader == null)
                {
                    this.logger.LogTrace("(-)[BLOCK_VALIDATION_ERROR]:false");
                    continue;
                }

                if (chainedHeader.ChainWork <= tip.ChainWork)
                    continue; // Can this happen POA TODO


                // TODO POA Log mined block
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.cancellation.Cancel();
            this.miningTask?.GetAwaiter().GetResult();

            this.cancellation.Dispose();
        }
    }
}
