using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.RestClients;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <inheritdoc cref="IMaturedBlocksSyncManager"/>
    public class MaturedBlocksSyncManager : IMaturedBlocksSyncManager
    {
        private readonly IDepositRepository depositRepository;

        private readonly IFederationGatewayClient federationGatewayClient;

        private readonly ILogger logger;

        private readonly CancellationTokenSource cancellation;

        private Task blockRequestingTask;

        /// <summary>The maximum amount of blocks to request at a time from alt chain.</summary>
        private const int MaxBlocksToRequest = 100;

        /// <summary>When we are fully synced we stop asking for more blocks for this amount of time.</summary>
        private const int RefreshDelayMs = 10_000;

        /// <summary>Delay between initialization and first request to other node.</summary>
        /// <remarks>Needed to give other node some time to start before bombing it with requests.</remarks>
        private const int InitializationDelayMs = 10_000;

        public MaturedBlocksSyncManager(DepositRepository depositStore, IFederationGatewayClient federationGatewayClient, ILoggerFactory loggerFactory)
        {
            this.depositRepository = depositStore;
            this.federationGatewayClient = federationGatewayClient;

            this.cancellation = new CancellationTokenSource();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            this.blockRequestingTask = this.RequestMaturedBlocksContinouslyAsync();
        }

        /// <summary>Continuously requests matured blocks from another chain.</summary>
        private async Task RequestMaturedBlocksContinouslyAsync()
        {
            try
            {
                // Initialization delay.
                // Give other node some time to start API service.
                await Task.Delay(InitializationDelayMs, this.cancellation.Token).ConfigureAwait(false);

                while (!this.cancellation.IsCancellationRequested)
                {
                    bool delayRequired = await this.SyncBatchOfBlocksAsync(this.cancellation.Token).ConfigureAwait(false);

                    if (delayRequired)
                    {
                        // Since we are synced or had a problem syncing there is no need to ask for more blocks right away.
                        // Therefore awaiting for a delay during which new block might be accepted on the alternative chain
                        // or alt chain node might be started.
                        await Task.Delay(RefreshDelayMs, this.cancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELLED]");
            }
        }

        /// <summary>Asks for blocks from another gateway node and then processes them.</summary>
        /// <returns><c>true</c> if delay between next time we should ask for blocks is required; <c>false</c> otherwise.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        protected async Task<bool> SyncBatchOfBlocksAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO investigate if we can ask for blocks that are reorgable. If so it's a problem and an attack vector.
            int blocksToRequest = MaxBlocksToRequest;
            int blockToStartFrom = this.depositRepository.GetSyncedBlockNumber();

            var model = new MaturedBlockRequestModel(blockToStartFrom, blocksToRequest);

            IList<MaturedBlockDepositsModel> matureBlockDeposits = await this.federationGatewayClient.GetMaturedBlockDepositsAsync(model, cancellationToken).ConfigureAwait(false);

            if (matureBlockDeposits != null)
            {
                if (matureBlockDeposits.Count == 0)
                {
                    // We're fully synced. May need to do something in the future?
                    return true;
                }

                // We found new deposits. Add them to the store.
                this.depositRepository.SaveDeposits(matureBlockDeposits);
            }

            // TODO: Do we need to update the most recently highest block

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();
            this.blockRequestingTask?.GetAwaiter().GetResult();
        }
    }
}
