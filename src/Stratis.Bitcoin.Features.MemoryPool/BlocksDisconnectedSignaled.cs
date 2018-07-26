﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Mempool observer on disconnected block notifications.
    /// </summary>
    public class BlocksDisconnectedSignaled : SignalObserver<Block>
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

        protected override void OnNextCore(Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            this.AddBackToMempoolAsync(block).ConfigureAwait(false).GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds Transactions in disconnected blocks back to the mempool.
        /// </summary>
        /// <remarks>This could potentially be optimised with an async queue.</remarks>
        /// <param name="block">The disconnected block containing the transactions.</param>
        /// <returns></returns>
        public async Task AddBackToMempoolAsync(Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            var state = new MempoolValidationState(true);

            await this.mempoolLock.WriteAsync(async () =>
            {
                this.logger.LogTrace("()");

                foreach (Transaction transaction in block.Transactions)
                {
                    bool success = await this.mempoolValidator.AcceptToMemoryPool(state, transaction);

                    if (!success)
                        this.logger.LogDebug("Transaction with hash:{0} failed to go back into mempool on block disconnect.", transaction.GetHash());
                    else
                        this.logger.LogTrace("Transaction with hash:{0} accepted back to mempool.");
                }

                this.logger.LogTrace("(-)");
            }).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }
    }
}   