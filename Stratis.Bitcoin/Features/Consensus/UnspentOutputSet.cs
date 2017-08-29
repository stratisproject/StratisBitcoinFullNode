using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.Features.Consensus
{
	public class UnspentOutputSet
	{
		Dictionary<uint256, UnspentOutputs> _Unspents;
		public TxOut GetOutputFor(TxIn txIn)
		{
			var unspent = this._Unspents.TryGet(txIn.PrevOut.Hash);
			if(unspent == null)
				return null;
			return unspent.TryGetOutput(txIn.PrevOut.N);
		}

		public bool HaveInputs(Transaction tx)
		{
			return tx.Inputs.All(txin => this.GetOutputFor(txin) != null);
		}

		public UnspentOutputs AccessCoins(uint256 uint256)
		{
			return this._Unspents.TryGet(uint256);
		}

		public Money GetValueIn(Transaction tx)
		{
			return tx.Inputs.Select(txin => this.GetOutputFor(txin).Value).Sum();
		}

		public void Update(Transaction tx, int height)
		{
			if(!tx.IsCoinBase)
				foreach(var input in tx.Inputs)
				{
					var c = this.AccessCoins(input.PrevOut.Hash);
					c.Spend(input.PrevOut.N);
				}
            this._Unspents.AddOrReplace(tx.GetHash(), new UnspentOutputs((uint)height, tx));
		}

        // TODO: coins argument should be of type UnspentOutputs[], not FetchCoinsResponse
        public void SetCoins(FetchCoinsResponse coins)
		{
            this._Unspents = new Dictionary<uint256, UnspentOutputs>(coins.UnspentOutputs.Length);
			foreach(var coin in coins.UnspentOutputs)
			{
				if(coin != null)
                    this._Unspents.Add(coin.TransactionId, coin);
			}
		}

        // TODO: coins argument should be of type UnspentOutputs[], not FetchCoinsResponse
        public void TrySetCoins(FetchCoinsResponse coins)
		{
            this._Unspents = new Dictionary<uint256, UnspentOutputs>(coins.UnspentOutputs.Length);
			foreach (var coin in coins.UnspentOutputs)
			{
				if (coin != null)
                    this._Unspents.TryAdd(coin.TransactionId, coin);
			}
		}

		public IEnumerable<UnspentOutputs> GetCoins(CoinView utxo)
		{
			return this._Unspents.Select(u => u.Value).ToList();
		}
	}
}
