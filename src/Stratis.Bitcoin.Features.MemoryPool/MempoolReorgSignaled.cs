using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Mempool observer on reorged block header notifications.
    /// </summary>
    public class MempoolReorgSignaled : SignalObserver<ChainedHeader>
    {
        private readonly IMempoolValidator mempoolValidator;
        private readonly MempoolSchedulerLock mempoolLock;
        private readonly ILogger logger;

        public MempoolReorgSignaled(IMempoolValidator mempoolValidator,
            MempoolSchedulerLock mempoolLock, ILoggerFactory loggerFactory)
        {
            this.mempoolValidator = mempoolValidator;
            this.mempoolLock = mempoolLock;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        protected override void OnNextCore(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader.HashBlock);

            this.AddBackToMempoolAsync(chainedHeader).GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        public async Task AddBackToMempoolAsync(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader.HashBlock);

            var state = new MempoolValidationState(true);

            await this.mempoolLock.WriteAsync(async () =>
            {
                foreach (Transaction transaction in chainedHeader.Block.Transactions)
                {
                    bool success = await this.mempoolValidator.AcceptToMemoryPool(state, transaction);
                    if (!success)
                    {
                        this.logger.LogTrace("(-)[REORG_RETURN_TX_TO_MEMPOOL_EXCEPTION]");
                        throw new ReturnReorgTransactionsToMempoolException(transaction.GetHash());
                    }
                }
            });

            this.logger.LogTrace("(-)");
        }
    }

    public class ReturnReorgTransactionsToMempoolException : Exception
    {
        public ReturnReorgTransactionsToMempoolException(uint256 transactionHash) : base(transactionHash.ToString())
        {
        }
    }
}