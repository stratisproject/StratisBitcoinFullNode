﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// This component is responsible retrieving signed multisig transactions (from <see cref="ICrossChainTransferStore"/>)
    /// and broadcasting them into the network.
    /// </summary>
    public interface ISignedMultisigTransactionBroadcaster
    {
        /// <summary>
        /// Broadcast signed transactions that are not in the mempool.
        /// </summary>
        /// <remarks>
        /// The current federated leader equal the <see cref="IFederationGatewaySettings.PublicKey"/> before it can broadcast the transactions.
        /// </remarks>
        Task BroadcastTransactionsAsync();

        /// <summary>
        /// Starts the broadcasting of fully signed transactions every N seconds.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the broadcasting of fully signed transactions.
        /// </summary>
        void Stop();
    }

    public class SignedMultisigTransactionBroadcaster : ISignedMultisigTransactionBroadcaster, IDisposable
    {
        /// <summary>
        /// How often to trigger the query for and broadcasting of new transactions.
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.TenSeconds;

        private readonly ILogger logger;
        private readonly ICrossChainTransferStore store;
        private readonly MempoolManager mempoolManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncProvider asyncProvider;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly IFederationWalletManager federationWalletManager;

        private IAsyncLoop asyncLoop;

        public SignedMultisigTransactionBroadcaster(
            IAsyncProvider asyncProvider,
            ILoggerFactory loggerFactory,
            ICrossChainTransferStore store,
            INodeLifetime nodeLifetime,
            MempoolManager mempoolManager,
            IBroadcasterManager broadcasterManager,
            IInitialBlockDownloadState ibdState,
            IFederationWalletManager federationWalletManager)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(store, nameof(store));
            Guard.NotNull(mempoolManager, nameof(mempoolManager));
            Guard.NotNull(broadcasterManager, nameof(broadcasterManager));


            this.asyncProvider = asyncProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.nodeLifetime = nodeLifetime;
            this.mempoolManager = mempoolManager;
            this.broadcasterManager = broadcasterManager;

            this.ibdState = ibdState;
            this.federationWalletManager = federationWalletManager;
        }

        /// <inheritdoc />
        public void Start()
        {
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(PartialTransactionRequester), _ =>
                {
                    this.BroadcastTransactionsAsync().GetAwaiter().GetResult();
                    return Task.CompletedTask;
                },
                this.nodeLifetime.ApplicationStopping,
                TimeBetweenQueries);
        }

        /// <inheritdoc />
        public async Task BroadcastTransactionsAsync()
        {
            if (this.ibdState.IsInitialBlockDownload() || !this.federationWalletManager.IsFederationWalletActive())
            {
                this.logger.LogTrace("Federation wallet isn't active or in IBD. Not attempting to broadcast signed transactions.");
                return;
            }

            ICrossChainTransfer[] transfers = this.store.GetTransfersByStatus(new CrossChainTransferStatus[]{CrossChainTransferStatus.FullySigned});

            if (!transfers.Any())
            {
                this.logger.LogTrace("Signed multisig transactions do not exist in the CrossChainTransfer store.");
                return;
            }

            foreach (ICrossChainTransfer transfer in transfers)
            {
                TxMempoolInfo txInfo = await this.mempoolManager.InfoAsync(transfer.PartialTransaction.GetHash()).ConfigureAwait(false);

                if (txInfo != null)
                {
                    this.logger.LogTrace("Deposit ID '{0}' already in the mempool.", transfer.DepositTransactionId);
                    continue;
                }

                this.logger.LogInformation("Broadcasting deposit-id={0} a signed multisig transaction {1} to the network.", transfer.DepositTransactionId, transfer.PartialTransaction.GetHash());

                await this.broadcasterManager.BroadcastTransactionAsync(transfer.PartialTransaction).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this.Stop();
            this.store?.Dispose();
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.asyncLoop != null)
            {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }
        }
    }
}
