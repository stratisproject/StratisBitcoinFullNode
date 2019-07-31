using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Features.FederatedPeg.Controllers;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Handles block syncing between gateways on 2 chains. This node will request
    /// blocks from another chain to look for cross chain deposit transactions.
    /// </summary>
    public interface IMaturedBlocksSyncManager : IDisposable
    {
        /// <summary>Starts requesting blocks from another chain.</summary>
        void Start();
    }

    /// <inheritdoc cref="IMaturedBlocksSyncManager"/>
    public class MaturedBlocksSyncManager : IMaturedBlocksSyncManager
    {
        private readonly ICrossChainTransferStore store;

        private readonly IFederationGatewayClient federationGatewayClient;
        private readonly IAsyncProvider asyncProvider;

        private readonly ILogger logger;

        private readonly CancellationTokenSource cancellation;

        private Task blockRequestingTask;

        /// <summary>The maximum amount of blocks to request at a time from alt chain.</summary>
        private const int MaxBlocksToRequest = 1000;

        /// <summary>The maximum amount of deposits to request at a time from alt chain.</summary>
        private const int MaxDepositsToRequest = 100;

        /// <summary>When we are fully synced we stop asking for more blocks for this amount of time.</summary>
        private const int RefreshDelayMs = 10_000;

        /// <summary>Delay between initialization and first request to other node.</summary>
        /// <remarks>Needed to give other node some time to start before bombing it with requests.</remarks>
        private const int InitializationDelayMs = 10_000;

        public MaturedBlocksSyncManager(ICrossChainTransferStore store, IFederationGatewayClient federationGatewayClient, ILoggerFactory loggerFactory, IAsyncProvider asyncProvider)
        {
            this.store = store;
            this.federationGatewayClient = federationGatewayClient;
            this.asyncProvider = asyncProvider;

            this.cancellation = new CancellationTokenSource();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Start()
        {
            this.blockRequestingTask = this.RequestMaturedBlocksContinouslyAsync();
            this.asyncProvider.RegisterTask($"{nameof(MaturedBlocksSyncManager)}.{nameof(this.blockRequestingTask)}", this.blockRequestingTask);
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
            int blocksToRequest = 1;

            // TODO why are we asking for max of 1 block and if it's not suspended then 1000? investigate this logic in maturedBlocksProvider
            if (!this.store.HasSuspended())
                blocksToRequest = MaxBlocksToRequest;

            // API method that provides blocks should't give us blocks that are not mature!
            var model = new MaturedBlockRequestModel(this.store.NextMatureDepositHeight, blocksToRequest, MaxDepositsToRequest);

            this.logger.LogDebug("Request model created: {0}:{1}, {2}:{3}.", nameof(model.BlockHeight), model.BlockHeight,
                nameof(model.MaxBlocksToSend), model.MaxBlocksToSend);

            // Ask for blocks.
            IList<MaturedBlockDepositsModel> matureBlockDeposits = await this.federationGatewayClient.GetMaturedBlockDepositsAsync(model, cancellationToken).ConfigureAwait(false);

            bool delayRequired = true;

            if (matureBlockDeposits != null)
            {
                // Log what we've received.
                foreach (MaturedBlockDepositsModel maturedBlockDeposit in matureBlockDeposits)
                {
                    // Order transactions in block deterministically
                    maturedBlockDeposit.Deposits = maturedBlockDeposit.Deposits.OrderBy(x => x.Id,
                        Comparer<uint256>.Create(DeterministicCoinOrdering.CompareUint256)).ToList();

                    foreach (IDeposit deposit in maturedBlockDeposit.Deposits)
                    {
                        this.logger.LogDebug("New deposit received BlockNumber={0}, TargetAddress='{1}', depositId='{2}', Amount='{3}'.",
                            deposit.BlockNumber, deposit.TargetAddress, deposit.Id, deposit.Amount);
                    }
                }

                if (matureBlockDeposits.Count > 0)
                {
                    RecordLatestMatureDepositsResult result = await this.store.RecordLatestMatureDepositsAsync(matureBlockDeposits).ConfigureAwait(false);

                    // If we received a portion of blocks we can ask for new portion without any delay.
                    if (result.MatureDepositRecorded)
                        delayRequired = false;
                }
                else
                {
                    this.logger.LogDebug("Considering ourselves fully synced since no blocks were received");

                    // If we've received nothing we assume we are at the tip and should flush.
                    // Same mechanic as with syncing headers protocol.
                    await this.store.SaveCurrentTipAsync().ConfigureAwait(false);
                }
            }
            else
                this.logger.LogDebug("Failed to fetch matured block deposits from counter chain node! {0} doesn't respond!", this.federationGatewayClient.EndpointUrl);

            return delayRequired;

        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();
            this.blockRequestingTask?.GetAwaiter().GetResult();
        }
    }
}