using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class SignedMultisigTransactionBroadcaster : ISignedMultisigTransactionBroadcaster, IDisposable
    {
        private readonly ILogger logger;
        private readonly IDisposable leaderReceiverSubscription;
        private readonly ICrossChainTransferStore store;
        private readonly string publicKey;
        private readonly MempoolManager mempoolManager;
        private readonly IBroadcasterManager broadcasterManager;

        public SignedMultisigTransactionBroadcaster(ILoggerFactory loggerFactory,
                                                    ICrossChainTransferStore store,
                                                    ILeaderReceiver leaderReceiver,
                                                    IFederationGatewaySettings settings,
                                                    MempoolManager mempoolManager,
                                                    IBroadcasterManager broadcasterManager)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(store, nameof(store));
            Guard.NotNull(leaderReceiver, nameof(leaderReceiver));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(mempoolManager, nameof(mempoolManager));
            Guard.NotNull(broadcasterManager, nameof(broadcasterManager));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.publicKey = settings.PublicKey;
            this.mempoolManager = mempoolManager;
            this.broadcasterManager = broadcasterManager;

            this.leaderReceiverSubscription = leaderReceiver.LeaderProvidersStream.Subscribe(async m => await BroadcastTransactionsAsync(m));
            this.logger.LogDebug("Subscribed to {0}", nameof(leaderReceiver), nameof(leaderReceiver.LeaderProvidersStream));
        }

        /// <inheritdoc />
        public async Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider)
        {
            if (this.publicKey != leaderProvider.CurrentLeader.ToString()) return;

            var transactions = await this.store.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).ConfigureAwait(false);

            if (!transactions.Any())
            {
                this.logger.LogTrace("Signed multisig transactions do not exist in the CrossChainTransfer store.");
                return;
            }

            foreach (var transaction in transactions)
            {
                var txInfo = await this.mempoolManager.InfoAsync(transaction.Key).ConfigureAwait(false);

                if (txInfo != null)
                {
                    this.logger.LogTrace("Transaction ID '{0}' already in the mempool.", transaction.Key);
                    continue;
                }

                await this.broadcasterManager.BroadcastTransactionAsync(transaction.Value).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this.store?.Dispose();
            this.leaderReceiverSubscription?.Dispose();
        }
    }
}
