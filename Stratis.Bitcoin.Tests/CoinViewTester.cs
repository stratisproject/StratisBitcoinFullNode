using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Consensus;
using NBitcoin;

namespace Stratis.Bitcoin.Tests
{
	public class CoinViewTester
	{
		public class SpendableCoin
		{
		}
		private CoinView coinView;

		public CoinViewTester(CoinView coinView)
		{
			this.coinView = coinView;
			_Hash = coinView.GetBlockHashAsync().Result;
		}

		List<UnspentOutputs> _PendingCoins = new List<UnspentOutputs>();
		public Coin[] CreateCoins(int coinCount)
		{
			var tx = new Transaction();
			tx.Outputs.AddRange(Enumerable.Range(0, coinCount)
							.Select(t => new TxOut(Money.Zero, new Key()))
							.ToArray());
			var output = new UnspentOutputs(1, tx);
			_PendingCoins.Add(output);
			return tx.Outputs.AsCoins().ToArray();
		}

		public bool Exists(Coin c)
		{
			var result = coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).Result;
			if(result.BlockHash != _Hash)
				throw new InvalidOperationException("Unexepected hash");
			if(result.UnspentOutputs[0] == null)
				return false;
			return result.UnspentOutputs[0].IsAvailable(c.Outpoint.N);
		}

		public void Spend(Coin c)
		{
			var coin = _PendingCoins.FirstOrDefault(u => u.TransactionId == c.Outpoint.Hash);
			if(coin == null)
			{
				var result = coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).Result;
				if(result.BlockHash != _Hash)
					throw new InvalidOperationException("Unexepected hash");
				if(result.UnspentOutputs[0] == null)
					throw new InvalidOperationException("Coin unavailable");

				if(!result.UnspentOutputs[0].Spend(c.Outpoint.N))
					throw new InvalidOperationException("Coin unspendable");
				_PendingCoins.Add(result.UnspentOutputs[0]);
			}
			else
			{
				if(!coin.Spend(c.Outpoint.N))
					throw new InvalidOperationException("Coin unspendable");
			}
		}

		uint256 _Hash;
		public uint256 NewBlock()
		{
			var newHash = new uint256(RandomUtils.GetBytes(32));
			coinView.SaveChangesAsync(_PendingCoins, null, _Hash, newHash).Wait();
			_PendingCoins.Clear();
			_Hash = newHash;
			return newHash;
		}

		public uint256 Rewind()
		{
			_Hash = coinView.Rewind().Result;
			return _Hash;
		}
	}
}
