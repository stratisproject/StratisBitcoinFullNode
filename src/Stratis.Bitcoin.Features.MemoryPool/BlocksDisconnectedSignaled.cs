using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Mempool observer on disconnected block notifications.
    /// </summary>
    public class BlocksDisconnectedSignaled : SignalObserver<ChainedHeaderBlock>
    {
        private readonly IMempoolValidator mempoolValidator;
        private readonly MempoolSchedulerLock mempoolLock;
        private readonly ILogger logger;

        public BlocksDisconnectedSignaled(IMempoolValidator mempoolValidator,
            MempoolSchedulerLock mempoolLock, ILoggerFactory loggerFactory)
        {
            this.mempoolValidator = mempoolValidator;
            this.mempoolLock = mempoolLock;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.AddBackToMempoolAsync(chainedHeaderBlock.Block).ConfigureAwait(false).GetAwaiter().GetResult();
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
                        this.logger.LogTrace("Transaction with hash '{0}' accepted back to mempool.", transaction.GetHash());
                }
                
            }).ConfigureAwait(false);
        }
    }
}