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
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private IAsyncLoop asyncLoop;

        public SignedMultisigTransactionBroadcaster(
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory,
            ICrossChainTransferStore store,
            INodeLifetime nodeLifetime,
            MempoolManager mempoolManager, IBroadcasterManager broadcasterManager)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(store, nameof(store));
            Guard.NotNull(mempoolManager, nameof(mempoolManager));
            Guard.NotNull(broadcasterManager, nameof(broadcasterManager));


            this.asyncLoopFactory = asyncLoopFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.nodeLifetime = nodeLifetime;
            this.mempoolManager = mempoolManager;
            this.broadcasterManager = broadcasterManager;
        }

        /// <inheritdoc />
        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(PartialTransactionRequester), _ =>
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
