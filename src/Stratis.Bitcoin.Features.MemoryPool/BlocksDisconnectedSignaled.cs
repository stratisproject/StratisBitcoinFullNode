using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Mempool observer on disconnected block notifications.
    /// </summary>
    public class BlocksDisconnectedSignaled : IDisposable
    {
        private readonly ITxMempool mempool;
        private readonly IMempoolValidator mempoolValidator;
        private readonly MempoolSchedulerLock mempoolLock;
        private readonly ISignals signals;
        private readonly ILogger logger;

        private SubscriptionToken blockDisconnectedSubscription;

        public BlocksDisconnectedSignaled(
            ITxMempool mempool,
            IMempoolValidator mempoolValidator,
            MempoolSchedulerLock mempoolLock,
            ILoggerFactory loggerFactory,
            ISignals signals)
        {
            this.mempool = mempool;
            this.mempoolValidator = mempoolValidator;
            this.mempoolLock = mempoolLock;
            this.signals = signals;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(this.OnBlockDisconnected);
        }

        private void OnBlockDisconnected(BlockDisconnected blockDisconnected)
        {
            this.RemoveInvalidTransactionsAsync(blockDisconnected.DisconnectedBlock.Block).ConfigureAwait(false).GetAwaiter().GetResult();
            this.AddBackToMempoolAsync(blockDisconnected.DisconnectedBlock.Block).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Adds Transactions in disconnected blocks back to the mempool.
        /// </summary>
        /// <remarks>This could potentially be optimized. with an async queue.</remarks>
        /// <param name="block">The disconnected block containing the transactions.</param>
        private async Task AddBackToMempoolAsync(Block block)
        {
            var state = new MempoolValidationState(true);

            await this.mempoolLock.WriteAsync(async () =>
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    if (transaction.IsProtocolTransaction())
                        continue;

                    bool success = await this.mempoolValidator.AcceptToMemoryPool(state, transaction);

                    if (!success)
                        this.logger.LogDebug("Transaction with hash '{0}' failed to go back into mempool on block disconnect.", transaction.GetHash());
                    else
                        this.logger.LogDebug("Transaction with hash '{0}' accepted back to mempool.", transaction.GetHash());
                }

            }).ConfigureAwait(false);
        }

        /// <summary>
        /// If there are any transactions in the mempool that depend on transactions no longer in the chain, remove them.
        /// </summary>
        private async Task RemoveInvalidTransactionsAsync(Block block)
        {
            // TODO: This was initially implemented only to fix a known issue on Cirrus.
            // There may be other things to validate mempool txs for when reorging:
            // - Do we need to check if coinstakes are being spent too?
            // - Are maturity requirements of transactions still met, in case the forked chain is shorter?
            // - Locktime requirements still met?
            // - Other unidentified cases where transactions may be rendered invalid by a reorg.

            if (!block.Transactions[0].IsCoinBase)
            {
                this.logger.LogWarning("Block '{0}' was disconnected and had no coinbase.", block.GetHash());
                return;
            }

            // Invalid transactions would have spent the coinbase. The other transactions can be put back into the mempool and be fine.
            uint256 coinbaseId = block.Transactions[0].GetHash();

            await this.mempoolLock.WriteAsync(async () =>
            {
                foreach (TxMempoolEntry mempoolEntry in this.mempool.MapTx.SpendsCoinbase.ToList())
                {
                    if (mempoolEntry.Transaction.Inputs.Any(x => x.PrevOut.Hash == coinbaseId))
                    {
                        this.logger.LogDebug("Removing transaction '{0}' from the mempool as it spends the coinbase transaction '{1}'", mempoolEntry.TransactionHash, coinbaseId);
                        this.mempool.RemoveRecursive(mempoolEntry.Transaction);
                    }

                }
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            this.signals.Unsubscribe(this.blockDisconnectedSubscription);
        }
    }
}