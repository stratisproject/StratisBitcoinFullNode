using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.Consensus
{
	public class CommitableCoinView : CoinView, IBackedCoinView
	{
		CoinView _Inner;
		HashSet<uint256> _ChangedCoins = new HashSet<uint256>();
		//Prunable coins should be flushed down inner
		InMemoryCoinView _Uncommited = new InMemoryCoinView() { RemovePrunableCoins = false };

		public override ChainedBlock Tip
		{
			get
			{
				return _Uncommited.Tip;
			}
		}

		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public CommitableCoinView(ChainedBlock newTip, CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			if(newTip == null)
				throw new ArgumentNullException("newTip");
			_Inner = inner;
			_Uncommited.SaveChanges(newTip, new[] { new UnspentOutputs(new uint256(), new Coins()) });
		}
		public CommitableCoinView(CoinView inner) : this(inner.Tip, inner)
		{
		}

		public UnspentOutputs AccessCoins(uint256 txId)
		{
			return _Uncommited.AccessCoins(txId);
		}

		public TxOut GetOutputFor(TxIn txIn)
		{
			return AccessCoins(txIn.PrevOut.Hash)?.TryGetOutput(txIn.PrevOut.N);
		}

		public Money GetValueIn(Transaction tx)
		{
			return tx
			.Inputs
			.Select(i => GetOutputFor(i).Value)
			.Sum();
		}

		public bool HaveInputs(Transaction tx)
		{
			foreach(var input in tx.Inputs)
			{
				var coin = AccessCoins(input.PrevOut.Hash);
				if(coin == null || !coin.IsAvailable(input.PrevOut.N))
					return false;
			}
			return true;
		}

		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			UnspentOutputs[] coins = new UnspentOutputs[txIds.Length];
			int i = 0;
			int notInCache = 0;
			foreach(var coin in _Uncommited.FetchCoins(txIds))
			{
				if(coin == null)
					notInCache++;
				coins[i++] = coin;
			}

			uint256[] txIds2 = new uint256[notInCache];
			i = 0;
			for(int ii = 0; ii < txIds.Length; ii++)
			{
				if(coins[ii] == null)
					txIds2[i++] = txIds[ii];
			}

			i = 0;


			foreach(var coin in Inner.FetchCoins(txIds2))
			{
				for(; i < coins.Length;)
				{
					if(coins[i] == null)
						break;
					i++;
				}
				if(i >= coins.Length)
					break;
				if(ReadThrough)
					_Uncommited.SaveChange(txIds[i], coin);
				coins[i] = coin;
				i++;
			}
			return coins;
		}


		bool _ReadThrough = true;
		public bool ReadThrough
		{
			get
			{
				return _ReadThrough;
			}
			set
			{
				_ReadThrough = value;
			}
		}

		public void SetInner(CoinView inner)
		{
			_Inner = inner;
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			foreach(var output in unspentOutputs)
				_ChangedCoins.Add(output.TransactionId);
			_Uncommited.SaveChanges(newTip, unspentOutputs);
		}
		public void Update(Transaction tx, int height)
		{
			_ChangedCoins.Add(tx.GetHash());
			_Uncommited.SaveChanges(tx, height);
		}

		public void Clear()
		{
			_Uncommited.Clear();
			_ChangedCoins.Clear();
		}

		public void Commit()
		{
			var changedCoins = GetChangedCoins();
			Inner.SaveChanges(_Uncommited.Tip, changedCoins);
		}

		private UnspentOutputs[] GetChangedCoins()
		{
			var changed = new UnspentOutputs[_ChangedCoins.Count];
			int i = 0;
			foreach(var kv in _Uncommited.coins)
			{
				if(_ChangedCoins.Contains(kv.Key))
				{
					changed[i++] = _Uncommited.coins[kv.Key];
				}
			}
			return changed;
		}

		public void Commit(CoinView coinview)
		{
			var changedCoins = GetChangedCoins();
			coinview.SaveChanges(_Uncommited.Tip, changedCoins);
		}
	}
}
