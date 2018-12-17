using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// Pushes <see cref="IMaturedBlockDeposits"/>s to <see cref="ICrossChainTransferStore"/>
    /// when they are received from <see cref="IMaturedBlockReceiver"/> and asks for more from <see cref="IMaturedBlocksRequester"/>.
    /// </summary>
    public class EventsPersister : IDisposable
    {
        private readonly ILogger logger;

        private readonly IDisposable maturedBlockDepositSubscription;

        private readonly ICrossChainTransferStore store;

        private readonly IMaturedBlocksRequester maturedBlocksRequester;

        private readonly object lockObj;

        private readonly Dictionary<int, DateTime> blockRequest;

        public EventsPersister(ILoggerFactory loggerFactory, ICrossChainTransferStore store, IMaturedBlockReceiver maturedBlockReceiver, IMaturedBlocksRequester maturedBlocksRequester)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.maturedBlocksRequester = maturedBlocksRequester;
            this.lockObj = new object();
            this.blockRequest = new Dictionary<int, DateTime>();

            this.maturedBlockDepositSubscription = maturedBlockReceiver.OnMaturedBlockDepositsPushed.Subscribe(this.PersistNewMaturedBlockDeposits);
            this.logger.LogDebug("Subscribed to {0}", nameof(maturedBlockReceiver), nameof(maturedBlockReceiver.OnMaturedBlockDepositsPushed));
        }

        private void PersistNewMaturedBlockDeposits(IMaturedBlockDeposits[] maturedBlockDeposits)
        {
            lock (this.lockObj)
            {
                foreach (IMaturedBlockDeposits maturedBlockDeposit in maturedBlockDeposits)
                {
                    foreach (IDeposit deposit in maturedBlockDeposit.Deposits)
                    {
                        this.logger.LogDebug("New deposit received BlockNumber={0} TargetAddress='{1}' depositId='{2}' Amount='{3}'.",
                            deposit.BlockNumber, deposit.TargetAddress, deposit.Id, deposit.Amount);
                    }
                }

                if (this.store.RecordLatestMatureDepositsAsync(maturedBlockDeposits).ConfigureAwait(false).GetAwaiter().GetResult())
                {
                    // There may be more blocks. Get them.
                    // Don't ask for the same blocks if the last time was less that 30 seconds ago.
                    if (!this.blockRequest.TryGetValue(this.store.NextMatureDepositHeight, out DateTime lastTime) || (lastTime.AddSeconds(30) < DateTime.Now))
                    {
                        this.maturedBlocksRequester.GetMoreBlocksAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        this.blockRequest[this.store.NextMatureDepositHeight] = DateTime.Now;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.store?.Dispose();
            this.maturedBlockDepositSubscription?.Dispose();
        }
    }
}
