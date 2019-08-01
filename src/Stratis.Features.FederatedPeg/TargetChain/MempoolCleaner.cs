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
using Stratis.Features.FederatedPeg.Wallet;

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
        private readonly MempoolOrphans mempoolOrphans;
        private readonly ICrossChainTransferStore store;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly IFederationWalletManager federationWalletManager;

        private IAsyncLoop asyncLoop;

        public MempoolCleaner(
            ILoggerFactory loggerFactory,
            MempoolOrphans mempoolOrphans,
            ICrossChainTransferStore crossChainTransferStore,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            IFederationWalletManager federationWalletManager)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.mempoolOrphans = Guard.NotNull(mempoolOrphans, nameof(mempoolOrphans));
            this.store = Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));
            this.asyncProvider = Guard.NotNull(asyncProvider, nameof(asyncProvider));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            this.federationWalletManager = Guard.NotNull(federationWalletManager, nameof(federationWalletManager));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        private IEnumerable<Transaction> CompletedTransactions(IEnumerable<Transaction> transactionsToCheck)
        {
            FederationWallet wallet = this.federationWalletManager.GetWallet();
            MultiSigTransactions walletTxs = wallet.MultiSigAddress.Transactions;

            HashSet<uint256> spendingTransactions = walletTxs
                .Where(t => t.SpendingDetails?.BlockHeight > 0)
                .Select(t => t.SpendingDetails?.TransactionId)
                .ToHashSet();

            foreach (Transaction tx in transactionsToCheck)
            {
                uint256 hash = tx.GetHash();

                // If there is a spendable output for this tx and it has a block height then the tx is in completed state.
                if (walletTxs.TryGetTransaction(hash, 0, out TransactionData tData) && tData.BlockHeight > 0)
                    yield return tx;
                // If this is a confirmed spending transaction then it is in completed state.
                else if (spendingTransactions.Contains(hash))
                    yield return tx;
                // If the tx has an input that is consumed by another confirmed transaction then it should be removed.
                else
                {
                    bool bDone = false;

                    foreach (TxIn txIn in tx.Inputs)
                    {
                        // Find the input's UTXO.
                        if (walletTxs.TryGetTransaction(txIn.PrevOut.Hash, (int)txIn.PrevOut.N, out TransactionData tData2))
                        {
                            // Check if the input's UTXO is being spent by another confirmed transaction.
                            if (tData2.SpendingDetails?.BlockHeight > 0 && tData2.SpendingDetails?.TransactionId != hash)
                            {
                                bDone = true;
                                break;
                            }
                        }
                    }

                    if (bDone)
                        yield return tx;
                }
            }
        }

        private async Task CleanMempoolAsync()
        {
            List<Transaction> transactionsToCheck = this.mempoolOrphans.OrphansList().Select(i => i.Tx).ToList();

            if (transactionsToCheck.Count == 0)
                return;

            List<Transaction> completedTransactions = null;

            this.federationWalletManager.Synchronous(() =>
            {
                completedTransactions = this.CompletedTransactions(transactionsToCheck).ToList();
            });

            List<Transaction> transactionsToRemove = this.store.CompletedWithdrawals(transactionsToCheck)
                .Union(completedTransactions)
                .ToList();

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
