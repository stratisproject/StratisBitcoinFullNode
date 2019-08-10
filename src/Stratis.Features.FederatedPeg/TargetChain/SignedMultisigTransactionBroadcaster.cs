using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Events;
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
        private readonly MempoolManager mempoolManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ISignals signals;
        private readonly ICrossChainTransferStore store;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly IFederationWalletManager federationWalletManager;
        private SubscriptionToken onCrossChainTransactionFullySignedSubscription;

        public SignedMultisigTransactionBroadcaster(
            ILoggerFactory loggerFactory,
            MempoolManager mempoolManager,
            IBroadcasterManager broadcasterManager,
            IInitialBlockDownloadState ibdState,
            IFederationWalletManager federationWalletManager,
            ISignals signals,
            ICrossChainTransferStore crossChainTransferStore = null)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.mempoolManager = Guard.NotNull(mempoolManager, nameof(mempoolManager));
            this.broadcasterManager = Guard.NotNull(broadcasterManager, nameof(broadcasterManager));
            this.ibdState = Guard.NotNull(ibdState, nameof(ibdState));
            this.federationWalletManager = Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.store = crossChainTransferStore;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        private async Task OnCrossChainTransactionFullySigned(CrossChainTransferTransactionFullySigned @event)
        {
            if (this.ibdState.IsInitialBlockDownload() || !this.federationWalletManager.IsFederationWalletActive())
            {
                this.logger.LogDebug("Federation wallet isn't active or in IBD. Not attempting to broadcast signed transactions.");
                return;
            }

            TxMempoolInfo txInfo = await this.mempoolManager.InfoAsync(@event.Transfer.PartialTransaction.GetHash()).ConfigureAwait(false);
            if (txInfo != null)
            {
                this.logger.LogDebug("Deposit ID '{0}' already in the mempool.", @event.Transfer.DepositTransactionId);
                return;
            }

            this.logger.LogDebug("Broadcasting deposit-id={0} a signed multisig transaction {1} to the network.", @event.Transfer.DepositTransactionId, @event.Transfer.PartialTransaction.GetHash());

            await this.broadcasterManager.BroadcastTransactionAsync(@event.Transfer.PartialTransaction).ConfigureAwait(false);

            // Check if transaction was actually added to a mempool.
            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(@event.Transfer.PartialTransaction.GetHash());

            if (transactionBroadCastEntry?.State == State.CantBroadcast && !CrossChainTransferStore.IsMempoolErrorRecoverable(transactionBroadCastEntry.MempoolError))
            {
                this.logger.LogWarning("Deposit ID '{0}' rejected due to '{1}'.", @event.Transfer.DepositTransactionId, transactionBroadCastEntry.ErrorMessage);
                this.store.RejectTransfer(@event.Transfer);
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            this.onCrossChainTransactionFullySignedSubscription = this.signals.Subscribe<CrossChainTransferTransactionFullySigned>(async (tx) => await this.OnCrossChainTransactionFullySigned(tx).ConfigureAwait(false));
        }

        public void Dispose()
        {
            this.Stop();
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.onCrossChainTransactionFullySignedSubscription != null)
            {
                this.signals.Unsubscribe(this.onCrossChainTransactionFullySignedSubscription);
            }
        }
    }
}
