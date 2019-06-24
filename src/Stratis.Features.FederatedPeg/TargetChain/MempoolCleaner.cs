using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// This component is responsible clearing completed withdrawals from the mempool.
    /// </summary>
    public interface IMempoolCleaner
    {
        /// <summary>
        /// Starts the cleaning of the mempool every N seconds.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the cleaning of the mempool.
        /// </summary>
        void Stop();
    }

    public class MempoolCleaner : IMempoolCleaner, IDisposable
    {
        /// <summary>
        /// How often to trigger the cleaning of the mempool.
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.Minute;
        private readonly ILogger logger;
        private readonly MempoolManager mempoolManager;
        private readonly MempoolOrphans mempoolOrphans;
        private readonly ICrossChainTransferStore store;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;

        private IAsyncLoop asyncLoop;

        public MempoolCleaner(
            ILoggerFactory loggerFactory,
            MempoolManager mempoolManager,
            MempoolOrphans mempoolOrphans,
            ICrossChainTransferStore crossChainTransferStore,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.mempoolManager = Guard.NotNull(mempoolManager, nameof(mempoolManager));
            this.mempoolOrphans = Guard.NotNull(mempoolOrphans, nameof(mempoolOrphans));
            this.store = Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));
            this.asyncProvider = Guard.NotNull(asyncProvider, nameof(asyncProvider));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        private async Task CleanMempoolAsync()
        {
            IEnumerable<Transaction> transactionsToCheck = (await this.mempoolManager.InfoAllAsync()).Select(i => i.Trx);

            List<Transaction> transactionsToRemove = this.store.CompletedWithdrawals(transactionsToCheck);

            if (transactionsToRemove.Count > 0)
            {
                this.mempoolOrphans.RemoveForBlock(transactionsToRemove);

                this.logger.LogDebug("Removed {0} transactions from mempool", transactionsToRemove.Count);
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(MempoolCleaner), async token => {
                await this.CleanMempoolAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            TimeBetweenQueries);
        }

        public void Dispose()
        {
            this.Stop();
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
