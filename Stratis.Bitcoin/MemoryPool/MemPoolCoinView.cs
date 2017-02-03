using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.Utils;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
    public class MempoolCoinView : CoinView , IBackedCoinView
    {
	    private readonly TxMempool memPool;
		private readonly SchedulerPairSession mempoolScheduler;

		public UnspentOutputSet Set { get; private set; }
	    public CoinView Inner { get; }

		public MempoolCoinView(CoinView inner, TxMempool memPool, SchedulerPairSession mempoolScheduler)
		{
			this.Inner = inner;
			this.memPool = memPool;
			this.mempoolScheduler = mempoolScheduler;
			this.Set = new UnspentOutputSet();
		}

	    public async Task LoadView(Transaction trx)
	    {
			// lookup all ids
		    var ids = trx.Inputs.Select(n => n.PrevOut.Hash).Append(trx.GetHash()).ToList();
			var coins = await this.Inner.FetchCoinsAsync(ids.ToArray());
			// find coins currently in the mempool
			var mempoolcoins = await this.mempoolScheduler.DoConcurrent(() =>
			{
				return this.memPool.MapTx.Values.Where(t => ids.Contains(t.TransactionHash)).Select(s => s.Transaction).ToList();
			});
			var memOutputs = mempoolcoins.Select(s => new UnspentOutputs(TxMempool.MempoolHeight, s));
			coins.UnspentOutputs = coins.UnspentOutputs.Concat(memOutputs).ToArray();
		    this.Set.SetCoins(coins);
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
			    Check.Assert(coins != null);
			    if (!coins.IsAvailable(txInput.PrevOut.N)) continue;
			    if (coins.Height <= nHeight)
			    {
				    dResult += (double) coins._Outputs[txInput.PrevOut.N].Value.Satoshi*(nHeight - coins.Height);
				    inChainInputValue += coins._Outputs[txInput.PrevOut.N].Value;

			    }
		    }
		    return ComputePriority(tx, dResult);
	    }

	    private double ComputePriority(Transaction trx, double dPriorityInputs, int nTxSize = 0)
	    {
		    nTxSize = MempoolValidator.CalculateModifiedSize(nTxSize, trx);
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
