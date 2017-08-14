using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class MempoolCoinView : CoinView , IBackedCoinView
    {
        private readonly TxMempool memPool;
        private readonly AsyncLock mempoolScheduler;
        private readonly IMempoolValidator mempoolValidator;

        public UnspentOutputSet Set { get; private set; }
        public CoinView Inner { get; }

        public MempoolCoinView(CoinView inner, TxMempool memPool, AsyncLock mempoolScheduler, IMempoolValidator mempoolValidator)
        {
            this.Inner = inner;
            this.memPool = memPool;
            this.mempoolScheduler = mempoolScheduler;
            this.mempoolValidator = mempoolValidator;
            this.Set = new UnspentOutputSet();
        }

        public async Task LoadView(Transaction trx)
        {
            // lookup all ids (duplicate ids are ignored in case a trx spends outputs from the same parent)
            var ids = trx.Inputs.Select(n => n.PrevOut.Hash).Distinct().Concat(new[] { trx.GetHash() }).ToList();
            var coins = await this.Inner.FetchCoinsAsync(ids.ToArray());
            // find coins currently in the mempool
            var mempoolcoins = await this.mempoolScheduler.ReadAsync(() =>
            {
                return this.memPool.MapTx.Values.Where(t => ids.Contains(t.TransactionHash)).Select(s => s.Transaction).ToList();
            });
            var memOutputs = mempoolcoins.Select(s => new UnspentOutputs(TxMempool.MempoolHeight, s));
            coins.UnspentOutputs = coins.UnspentOutputs.Concat(memOutputs).ToArray();

            // the UTXO set might have been updated with a recently received block 
            // but the block has not yet arrived to the mempool and remove the pending trx
            // from the pool (a race condition), block validation doesn't lock the mempool.
            // its safe to ignore duplicats on the UTXO set as duplicates mean a trx is in 
            // a block and the block will soon remove the trx from the pool.
            this.Set.TrySetCoins(coins);
        }

        public UnspentOutputs GetCoins(uint256 txid)
        {
            return this.Set.AccessCoins(txid);
        }

        public bool HaveCoins(uint256 txid)
        {
            if (this.memPool.Exists(txid))
                return true;

            return this.Set.AccessCoins(txid) != null;
        }

        public double GetPriority(Transaction tx, int nHeight, Money inChainInputValue)
        {
            inChainInputValue = 0;
            if (tx.IsCoinBase)
                return 0.0;
            double dResult = 0.0;
            foreach (var txInput in tx.Inputs)
            {
                var coins = this.Set.AccessCoins(txInput.PrevOut.Hash);
                Guard.Assert(coins != null);
                if (!coins.IsAvailable(txInput.PrevOut.N)) continue;
                if (coins.Height <= nHeight)
                {
                    dResult += (double) coins._Outputs[txInput.PrevOut.N].Value.Satoshi*(nHeight - coins.Height);
                    inChainInputValue += coins._Outputs[txInput.PrevOut.N].Value;

                }
            }
            return this.ComputePriority(tx, dResult);
        }

        private double ComputePriority(Transaction trx, double dPriorityInputs, int nTxSize = 0)
        {
            nTxSize = MempoolValidator.CalculateModifiedSize(nTxSize, trx, this.mempoolValidator.ConsensusOptions);
            if (nTxSize == 0) return 0.0;

            return dPriorityInputs/nTxSize;
        }

        public bool SpendsCoinBase(Transaction tx)
        {
            foreach (var txInput in tx.Inputs)
            {
                var coins = this.Set.AccessCoins(txInput.PrevOut.Hash);
                if (coins.IsCoinbase)
                    return true;
            }

            return false;
        }

        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash,
            uint256 nextBlockHash)
        {
            throw new NotImplementedException();
        }

        public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
        {
            throw new NotImplementedException();
        }

        public bool HaveInputs(Transaction tx)
        {
            return this.Set.HaveInputs(tx);
        }

        public Money GetValueIn(Transaction tx)
        {
            return this.Set.GetValueIn(tx);
        }
            
        public TxOut GetOutputFor(TxIn input)
        {
            return this.Set.GetOutputFor(input);
        }

        public override Task<uint256> Rewind()
        {
            throw new NotImplementedException();
        }

    }
}
