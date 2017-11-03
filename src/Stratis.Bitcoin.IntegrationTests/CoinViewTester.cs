using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.IntegrationTests
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
            this._Hash = coinView.GetBlockHashAsync().Result;
		}

		List<UnspentOutputs> _PendingCoins = new List<UnspentOutputs>();
		public Coin[] CreateCoins(int coinCount)
		{
			var tx = new Transaction();
			tx.Outputs.AddRange(Enumerable.Range(0, coinCount)
							.Select(t => new TxOut(Money.Zero, new Key()))
							.ToArray());
			var output = new UnspentOutputs(1, tx);
            this._PendingCoins.Add(output);
			return tx.Outputs.AsCoins().ToArray();
		}

		public bool Exists(Coin c)
		{
			var result = this.coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).Result;
			if(result.BlockHash != this._Hash)
				throw new InvalidOperationException("Unexepected hash");
			if(result.UnspentOutputs[0] == null)
				return false;
			return result.UnspentOutputs[0].IsAvailable(c.Outpoint.N);
		}

		public void Spend(Coin c)
		{
			var coin = this._PendingCoins.FirstOrDefault(u => u.TransactionId == c.Outpoint.Hash);
			if(coin == null)
			{
				var result = this.coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).Result;
				if(result.BlockHash != this._Hash)
					throw new InvalidOperationException("Unexepected hash");
				if(result.UnspentOutputs[0] == null)
					throw new InvalidOperationException("Coin unavailable");

				if(!result.UnspentOutputs[0].Spend(c.Outpoint.N))
					throw new InvalidOperationException("Coin unspendable");
                this._PendingCoins.Add(result.UnspentOutputs[0]);
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
			this.coinView.SaveChangesAsync(this._PendingCoins, null, this._Hash, newHash).Wait();
			this._PendingCoins.Clear();
            this._Hash = newHash;
			return newHash;
		}

		public uint256 Rewind()
		{
            this._Hash = this.coinView.Rewind().Result;
			return this._Hash;
		}
	}
}
