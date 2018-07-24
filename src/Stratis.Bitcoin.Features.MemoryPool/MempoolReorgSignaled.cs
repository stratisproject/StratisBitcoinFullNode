using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Mempool observer on reorged block header notifications.
    /// </summary>
    public class MempoolReorgSignaled : SignalObserver<ChainedHeader>
    {
        private readonly ITxMempool mempool;
        private readonly IMempoolValidator mempoolValidator;
        private readonly MempoolSchedulerLock mempoolLock;

        public MempoolReorgSignaled(ITxMempool mempool, IMempoolValidator mempoolValidator,
            MempoolSchedulerLock mempoolLock)
        {
            this.mempool = mempool;
            this.mempoolValidator = mempoolValidator;
            this.mempoolLock = mempoolLock;
        }

        protected override void OnNextCore(ChainedHeader chainedHeader)
        {
            Task addBackToMempool = this.AddBackToMempoolAsync(chainedHeader);

            //TODO: sync until Signaler async
            addBackToMempool.GetAwaiter().GetResult();
        }

        public async Task AddBackToMempoolAsync(ChainedHeader chainedHeader)
        {
            var state = new MempoolValidationState(true);

            await this.mempoolLock.WriteAsync(async () =>
            {
                foreach (Transaction transaction in chainedHeader.Block.Transactions)
                {
                    await this.mempoolValidator.AcceptToMemoryPool(state, transaction);
                }
            });
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}