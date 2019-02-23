using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
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
        /// <param name="leaderProvider">
        /// The current federated leader.
        /// </param>
        /// <remarks>
        /// The current federated leader equal the <see cref="IFederationGatewaySettings.PublicKey"/> before it can broadcast the transactions.
        /// </remarks>
        Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider);
    }

    public class SignedMultisigTransactionBroadcaster : ISignedMultisigTransactionBroadcaster, IDisposable
    {
        private readonly ILogger logger;
        private readonly IDisposable leaderReceiverSubscription;
        private readonly ICrossChainTransferStore store;
        private readonly string publicKey;
        private readonly MempoolManager mempoolManager;
        private readonly IBroadcasterManager broadcasterManager;

        public SignedMultisigTransactionBroadcaster(ILoggerFactory loggerFactory, ICrossChainTransferStore store, ILeaderReceiver leaderReceiver, IFederationGatewaySettings settings,
            MempoolManager mempoolManager, IBroadcasterManager broadcasterManager)
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

            this.leaderReceiverSubscription = leaderReceiver.LeaderProvidersStream.Subscribe(async m => await this.BroadcastTransactionsAsync(m).ConfigureAwait(false));
            this.logger.LogDebug("Subscribed to {0}", nameof(leaderReceiver), nameof(leaderReceiver.LeaderProvidersStream));
        }

        /// <inheritdoc />
        public async Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider)
        {
            if (this.publicKey != leaderProvider.CurrentLeaderKey.ToString()) return;

            Dictionary<uint256, Transaction> transactions = await this.store.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).ConfigureAwait(false);

            if (!transactions.Any())
            {
                this.logger.LogTrace("Signed multisig transactions do not exist in the CrossChainTransfer store.");
                return;
            }

            foreach (KeyValuePair<uint256, Transaction> transaction in transactions)
            {
                TxMempoolInfo txInfo = await this.mempoolManager.InfoAsync(transaction.Value.GetHash()).ConfigureAwait(false);

                if (txInfo != null)
                {
                    this.logger.LogTrace("Transaction ID '{0}' already in the mempool.", transaction.Key);
                    continue;
                }

                this.logger.LogInformation("Broadcasting deposit-id={0} a signed multisig transaction {1} to the network.", transaction.Key, transaction.Value.GetHash());

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
