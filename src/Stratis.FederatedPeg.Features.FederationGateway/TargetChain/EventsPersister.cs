using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class EventsPersister : IEventPersister, IDisposable
    {
        private readonly ILogger logger;

        private readonly IDisposable maturedBlockDepositSubscription;

        private readonly ICrossChainTransferStore store;

        private readonly IMaturedBlocksRequester maturedBlocksRequester;

        private readonly object lockObj;

        public EventsPersister(ILoggerFactory loggerFactory,
                               ICrossChainTransferStore store,
                               IMaturedBlockReceiver maturedBlockReceiver,
                               IMaturedBlocksRequester maturedBlocksRequester)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.maturedBlocksRequester = maturedBlocksRequester;
            this.lockObj = new object();

            this.maturedBlockDepositSubscription = maturedBlockReceiver.MaturedBlockDepositStream.Subscribe(async m => PersistNewMaturedBlockDeposits(m) );
            this.logger.LogDebug("Subscribed to {0}", nameof(maturedBlockReceiver), nameof(maturedBlockReceiver.MaturedBlockDepositStream));
        }

        /// <inheritdoc />
        public void PersistNewMaturedBlockDeposits(IMaturedBlockDeposits[] maturedBlockDeposits)
        {
            lock (this.lockObj)
            {
                this.logger.LogDebug("New {0} received.", nameof(IMaturedBlockDeposits));

                if (this.store.RecordLatestMatureDepositsAsync(maturedBlockDeposits).ConfigureAwait(false).GetAwaiter().GetResult())
                {
                    // There may be more blocks. Get them.
                    this.maturedBlocksRequester.GetMoreBlocksAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
        }

        /// <inheritdoc />
        public Task PersistNewSourceChainTip(IBlockTip newTip)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.store?.Dispose();
            this.maturedBlockDepositSubscription?.Dispose();
        }
    }
}
