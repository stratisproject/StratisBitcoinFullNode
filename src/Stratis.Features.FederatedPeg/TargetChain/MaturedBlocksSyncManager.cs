using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Controllers;
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
        private readonly ICrossChainTransferStore crossChainTransferStore;

        private readonly IFederationGatewayClient federationGatewayClient;
        private readonly IAsyncProvider asyncProvider;

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

        public MaturedBlocksSyncManager(ICrossChainTransferStore store, IFederationGatewayClient federationGatewayClient, ILoggerFactory loggerFactory, IAsyncProvider asyncProvider)
        {
            this.crossChainTransferStore = store;
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
        /// <param name="cancellationToken">A cancellation token to ensure that the task exists should it take too long.</param>
        /// <returns><c>true</c> if delay between next time we should ask for blocks is required; <c>false</c> otherwise.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        protected async Task<bool> SyncBatchOfBlocksAsync(CancellationToken cancellationToken)
        {
            int blocksToRequest = 1;

            // TODO why are we asking for max of 1 block and if it's not suspended then 1000? investigate this logic in maturedBlocksProvider
            if (!this.crossChainTransferStore.HasSuspended())
                blocksToRequest = MaxBlocksToRequest;

            // API method that provides blocks should't give us blocks that are not mature!
            var model = new MaturedBlockRequestModel(this.crossChainTransferStore.NextMatureDepositHeight, blocksToRequest);

            this.logger.LogDebug("Request model created: {0}:{1}, {2}:{3}.", nameof(model.BlockHeight), model.BlockHeight, nameof(model.MaxBlocksToSend), model.MaxBlocksToSend);

            // Ask for blocks.
            ApiResult<List<MaturedBlockDepositsModel>> matureBlockDepositsResult = await this.federationGatewayClient.GetMaturedBlockDepositsAsync(model, cancellationToken).ConfigureAwait(false);

            if (matureBlockDepositsResult == null)
            {
                this.logger.LogInformation("Unable to fetch mature block deposits from the counter chain node; {0} didn't respond.", this.federationGatewayClient.EndpointUrl);
                this.logger.LogTrace("(-)[COUNTER_CHAIN_NODE_UNRESPONSIVE]:true");
                return true;
            }

            if (matureBlockDepositsResult.Value == null)
            {
                this.logger.LogInformation("Unable to fetch mature block deposits from the counter chain node; mature deposits was null.");
                this.logger.LogTrace("(-)[COUNTER_CHAIN_NODE_RESULT_NULL]:true");
                return true;
            }

            if (!matureBlockDepositsResult.Succeeded)
            {
                this.logger.LogInformation("Unable to fetch mature block deposits from the counter chain node; {0}.", matureBlockDepositsResult.ErrorMessage);
                this.logger.LogTrace("(-)[COUNTER_CHAIN_NODE_ERROR]:true,{0}={1}", nameof(matureBlockDepositsResult.ErrorMessage), matureBlockDepositsResult.ErrorMessage);
                return true;
            }

            // Log what we've received.
            foreach (MaturedBlockDepositsModel maturedBlockDeposit in matureBlockDepositsResult.Value)
            {
                // Order transactions in block deterministically
                maturedBlockDeposit.Deposits = maturedBlockDeposit.Deposits.OrderBy(x => x.Id, Comparer<uint256>.Create(DeterministicCoinOrdering.CompareUint256)).ToList();

                foreach (IDeposit deposit in maturedBlockDeposit.Deposits)
                {
                    this.logger.LogDebug("New deposit received BlockNumber={0}, TargetAddress='{1}', depositId='{2}', Amount='{3}'.", deposit.BlockNumber, deposit.TargetAddress, deposit.Id, deposit.Amount);
                }
            }

            if (matureBlockDepositsResult.Value.Count > 0)
            {
                RecordLatestMatureDepositsResult result = await this.crossChainTransferStore.RecordLatestMatureDepositsAsync(matureBlockDepositsResult.Value).ConfigureAwait(false);

                // If we received a portion of blocks we can ask for new portion without any delay.
                if (result.MatureDepositRecorded)
                {
                    this.logger.LogTrace("(-)[MATURE_DEPOSIT_RECORDED]:false");
                    return false;
                }
            }
            else
            {
                this.logger.LogDebug("Considering ourselves fully synced since no blocks were received.");

                // If we've received nothing we assume we are at the tip and should flush.
                // Same mechanic as with syncing headers protocol.
                await this.crossChainTransferStore.SaveCurrentTipAsync().ConfigureAwait(false);
            }

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