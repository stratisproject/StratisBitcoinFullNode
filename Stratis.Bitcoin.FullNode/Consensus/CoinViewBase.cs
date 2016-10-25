using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.FullNode.Consensus
{
    public class CoinViewBase
    {
		public ChainedBlock Tip
		{
			get;
			set;
		}
		public Coins AccessCoins(uint256 txId)
        {
			Coins r;
			coins.TryGetValue(txId, out r);
			return r;
        }

		public bool HaveInputs(Transaction tx)
		{
			return tx
			.Inputs
			.Select(i => GetOutputFor(i))
			.All(o => o != null);
		}

		public TxOut GetOutputFor(TxIn txIn)
		{
			var c = AccessCoins(txIn.PrevOut.Hash);
			if(c == null)
				return null;
			if(txIn.PrevOut.N >= c.Outputs.Count || txIn.PrevOut.N >= int.MaxValue)
				return null;
			return c.Outputs[(int)txIn.PrevOut.N];
		}

		Dictionary<uint256, Coins> coins = new Dictionary<uint256, Coins>();
		internal void Update(Transaction tx, int height)
		{
			tx = tx.Clone();
			coins.AddOrReplace(tx.GetHash(), new Coins(tx, height));
			if(!tx.IsCoinBase)
			{
				foreach(var input in tx.Inputs)
				{
					var c = AccessCoins(input.PrevOut.Hash);
					c.Spend((int)input.PrevOut.N);
					if(c.IsPruned)
						coins.Remove(input.PrevOut.Hash);
				}
			}
		}

		internal Money GetValueIn(Transaction tx)
		{
			return tx
			.Inputs
			.Select(i => GetOutputFor(i).Value)
			.Sum();
		}

		public void AcceptChanges(ChainedBlock newTip)
		{
			Tip = newTip;
		}

		public void RejectChanges()
		{
			
		}

		public void Warmup(IEnumerable<OutPoint> impactedOutpoints)
		{
			
		}
	}
}
