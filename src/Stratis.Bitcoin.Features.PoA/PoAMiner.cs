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
    // TODO POA comment
    public interface IPoAMiner : IDisposable
    {
        void InitializeMining();
    }

    public class PoAMiner : IPoAMiner
    {
        // TODO POA. Implement miner properly later. Right now we need bare minimum to test syncing and payloads.

        private const bool MineDuringIBD = true; // TODO POA just for tests

        private readonly IConsensusManager consensusManager;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ILogger logger;

        private readonly Network network;

        /// <summary>
        /// A cancellation token source that can cancel the mining processes and is linked to the <see cref="INodeLifetime.ApplicationStopping"/>.
        /// </summary>
        private CancellationTokenSource cancellation;

        private readonly IInitialBlockDownloadState ibdState;

        private readonly PoABlockDefinition blockDefinition;

        private Task miningTask;

        public PoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            PoABlockDefinition blockDefinition)
        {
            this.consensusManager = consensusManager;
            this.dateTimeProvider = dateTimeProvider;
            this.network = network;
            this.ibdState = ibdState;
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
                try
                {
                    // Don't mine in IBD.
                    if (this.ibdState.IsInitialBlockDownload() && !MineDuringIBD)
                    {
                        int attemptDelayMs = 20_000;
                        await Task.Delay(attemptDelayMs, this.cancellation.Token).ConfigureAwait(false);

                        continue;
                    }


                    // TODO POA check if our timestamp and if not wait for our timestamp (wait with cancellation token)

                    // TODO will set nonce and target to const. Check todo in PoABlockDefenition

                    ChainedHeader tip = this.consensusManager.Tip;

                    BlockTemplate blockTemplate = this.blockDefinition.Build(tip);

                    // Timestamp should only greater than prev one.
                    if (blockTemplate.Block.Header.Time <= tip.Header.Time)
                        continue; // TODO POA Log

                    // TODO sign the block with our signature

                    // Update merkle root.
                    blockTemplate.Block.UpdateMerkleRoot();

                    ChainedHeader chainedHeader = await this.consensusManager.BlockMinedAsync(blockTemplate.Block).ConfigureAwait(false); // TODO POA That should also do interg vaidation

                    if (chainedHeader == null)
                    {
                        this.logger.LogTrace("(-)[BLOCK_VALIDATION_ERROR]:false");
                        continue;
                    }

                    // TODO POA Log mined block
                }
                catch (OperationCanceledException)
                {
                }
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
