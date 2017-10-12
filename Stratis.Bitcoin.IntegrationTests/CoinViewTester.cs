using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using System.Threading.Tasks;

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
		}

        public async Task InitializeAsync()
        {
            this._Hash = await this.coinView.GetBlockHashAsync().ConfigureAwait(false);
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

		public async Task<bool> ExistsAsync(Coin c)
		{
			var result = await this.coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).ConfigureAwait(false);
			if(result.BlockHash != this._Hash)
				throw new InvalidOperationException("Unexepected hash");
			if(result.UnspentOutputs[0] == null)
				return false;
			return result.UnspentOutputs[0].IsAvailable(c.Outpoint.N);
		}

		public async Task SpendAsync(Coin c)
		{
			var coin = this._PendingCoins.FirstOrDefault(u => u.TransactionId == c.Outpoint.Hash);
			if(coin == null)
			{
				var result = await this.coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).ConfigureAwait(false);
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
		public async Task<uint256> NewBlockAsync()
		{
			var newHash = new uint256(RandomUtils.GetBytes(32));
			await this.coinView.SaveChangesAsync(this._PendingCoins, null, this._Hash, newHash).ConfigureAwait(false);
			this._PendingCoins.Clear();
            this._Hash = newHash;
			return newHash;
		}

		public async Task<uint256> RewindAsync()
		{
            this._Hash = await this.coinView.Rewind().ConfigureAwait(false);
			return this._Hash;
		}
	}
}
