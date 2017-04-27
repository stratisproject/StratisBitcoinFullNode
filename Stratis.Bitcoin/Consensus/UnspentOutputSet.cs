using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
	public class UnspentOutputSet
	{
		Dictionary<uint256, UnspentOutputs> _Unspents;
		public TxOut GetOutputFor(TxIn txIn)
		{
			var unspent = _Unspents.TryGet(txIn.PrevOut.Hash);
			if(unspent == null)
				return null;
			return unspent.TryGetOutput(txIn.PrevOut.N);
		}

		public bool HaveInputs(Transaction tx)
		{
			return tx.Inputs.All(txin => GetOutputFor(txin) != null);
		}

		public UnspentOutputs AccessCoins(uint256 uint256)
		{
			return _Unspents.TryGet(uint256);
		}

		public Money GetValueIn(Transaction tx)
		{
			return tx.Inputs.Select(txin => GetOutputFor(txin).Value).Sum();
		}

		public void Update(Transaction tx, int height)
		{
			if(!tx.IsCoinBase)
				foreach(var input in tx.Inputs)
				{
					var c = AccessCoins(input.PrevOut.Hash);
					c.Spend(input.PrevOut.N);
				}
			_Unspents.AddOrReplace(tx.GetHash(), new UnspentOutputs((uint)height, tx));
		}

		public void SetCoins(FetchCoinsResponse coins)
		{
			_Unspents = new Dictionary<uint256, UnspentOutputs>(coins.UnspentOutputs.Length);
			foreach(var coin in coins.UnspentOutputs)
			{
				if(coin != null)
					_Unspents.Add(coin.TransactionId, coin);
			}
		}

		public void TrySetCoins(FetchCoinsResponse coins)
		{
			_Unspents = new Dictionary<uint256, UnspentOutputs>(coins.UnspentOutputs.Length);
			foreach (var coin in coins.UnspentOutputs)
			{
				if (coin != null)
					_Unspents.TryAdd(coin.TransactionId, coin);
			}
		}

		public IEnumerable<UnspentOutputs> GetCoins(CoinView utxo)
		{
			return _Unspents.Select(u => u.Value).ToList();
		}
	}
}
