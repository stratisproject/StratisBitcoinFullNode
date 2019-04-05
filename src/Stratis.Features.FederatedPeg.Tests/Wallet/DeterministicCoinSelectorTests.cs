using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class DeterministicCoinSelectorTests
    {
        private readonly DeterministicCoinSelector deterministicCoinSelector;
        private readonly Network network;

        public DeterministicCoinSelectorTests()
        {
            this.deterministicCoinSelector = new DeterministicCoinSelector();
            this.network = KnownNetworks.StratisMain;
        }

        [Fact]
        public void NotEnoughFundsReturnNull()
        {
            Transaction fundingTransaction = this.network.CreateTransaction();
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.9m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.09m), new Key().ScriptPubKey));

            Money target = new Money(1m, MoneyUnit.BTC);

            var coins = new List<ICoin>
            {
                new Coin(fundingTransaction, 0),
                new Coin(fundingTransaction, 1)
            };

            IEnumerable<ICoin> result = this.deterministicCoinSelector.Select(coins, target);
            Assert.Null(result);
        }

        [Fact]
        public void UseOneCoinWhenAmountMatchesExactly()
        {
            Transaction fundingTransaction = this.network.CreateTransaction();
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.5m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1.5m), new Key().ScriptPubKey));

            Money target = new Money(1.5m, MoneyUnit.BTC);

            var coins = new List<ICoin>
            {
                new Coin(fundingTransaction, 0),
                new Coin(fundingTransaction, 1),
                new Coin(fundingTransaction, 2)
            };

            IEnumerable<ICoin> result = this.deterministicCoinSelector.Select(coins, target);
            Assert.Single(result);
            Assert.Equal(Money.Coins(1.5m), result.First().Amount);
        }

        [Fact]
        public void UseSeveralCoinsWhenTheirTotalOverTarget()
        {
            Transaction fundingTransaction = this.network.CreateTransaction();
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.5m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.4m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.6m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1.1m), new Key().ScriptPubKey));

            Money target = new Money(1m, MoneyUnit.BTC);

            var coins = new List<ICoin>
            {
                new Coin(fundingTransaction, 0),
                new Coin(fundingTransaction, 1),
                new Coin(fundingTransaction, 2),
                new Coin(fundingTransaction, 3)
            };

            ICoin[] result = this.deterministicCoinSelector.Select(coins, target).ToArray();
            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void UseOneCoinWhenLowerCoinsDontTotalTarget()
        {
            Transaction fundingTransaction = this.network.CreateTransaction();
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.1m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.2m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.3m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1.1m), new Key().ScriptPubKey));

            Money target = new Money(1m, MoneyUnit.BTC);

            var coins = new List<ICoin>
            {
                new Coin(fundingTransaction, 0),
                new Coin(fundingTransaction, 1),
                new Coin(fundingTransaction, 2),
                new Coin(fundingTransaction, 3)
            };

            ICoin[] result = this.deterministicCoinSelector.Select(coins, target).ToArray();
            Assert.Single(result);
        }

        [Fact]
        public void OutputsWithSameAmountsIncludedDeterministically()
        {
            Transaction fundingTransaction = this.network.CreateTransaction();
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.6m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(0.3m), new Key().ScriptPubKey));
            fundingTransaction.Outputs.Add(new TxOut(Money.Coins(1m), new Key().ScriptPubKey));

            Money target = new Money(2.9m, MoneyUnit.BTC);

            var coins = new List<ICoin>
            {
                new Coin(fundingTransaction, 0),
                new Coin(fundingTransaction, 1),
                new Coin(fundingTransaction, 2),
                new Coin(fundingTransaction, 3),
                new Coin(fundingTransaction, 4)
            };

            ICoin[] result = this.deterministicCoinSelector.Select(coins, target).ToArray();

            // Lets do this 100 times and ensure the result is the same every time.
            for (int i = 0; i < 100; i++)
            {
                ICoin[] result2 = this.deterministicCoinSelector.Select(coins, target).ToArray();

                Assert.Equal(result.Length, result2.Count());

                for (int j = 0; j < result.Length; j++)
                {
                    Assert.Equal(result[j].Outpoint.N, result2[j].Outpoint.N);
                }
            }
        }

    }
}
