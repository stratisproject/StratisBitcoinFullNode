using System;
using System.Threading.Tasks;
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

            this.AddBackToMempoolAsync(block).GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        public async Task AddBackToMempoolAsync(Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            var state = new MempoolValidationState(true);

            await this.mempoolLock.WriteAsync(async () =>
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    bool success = await this.mempoolValidator.AcceptToMemoryPool(state, transaction);
                    this.logger.LogTrace("[ACCEPT_TO_MEMPOOL]:", success);
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