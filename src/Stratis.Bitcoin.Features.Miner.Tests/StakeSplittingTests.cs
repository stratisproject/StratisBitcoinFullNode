using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Miner.Staking;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class StakeSplittingTests : PosMintingTest
    {
        private const int ChainHeight = 500000;

        public StakeSplittingTests()
        {
            this.network.Consensus.Options = new PosConsensusOptions();
        }

        [Fact]
        public void Given_A_Wallet_With_Big_And_Small_Coins_Then_Big_Coins_Below_Target_Should_Get_Split()
        {
            // Aiming for target value around 4700.
            var amounts = new[] { 2000, 10 }
                .Concat(Enumerable.Repeat(100_000, 3))
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToArray();

            var shouldStakeSplitForThe100000Coin = this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Length,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Last(),
                chainHeight: ChainHeight);

            shouldStakeSplitForThe100000Coin.Should().BeTrue("coin is bigger than target average value");
        }

        [Fact]
        public void Given_A_Wallet_With_A_Few_Balanced_Coins_Then_Big_Coins_Below_Target_Should_Get_Split()
        {
            var amounts = Enumerable.Repeat(900, 40)
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToArray();

            var shouldStakeSplit = this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Length,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Last(),
                chainHeight: ChainHeight);

            shouldStakeSplit.Should().BeTrue("coin is bigger than target average value");
        }

        [Fact]
        public void Given_A_Wallet_With_Big_And_Small_Coins_Then_Coins_Below_Target_Should_Get_Split()
        {
            // Aiming for target value around 4700.
            var amounts = new[] { 2000, 10 }
                .Concat(Enumerable.Repeat(100_000, 3))
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToArray();

            var shouldStakeSplitForThe2000Coin = this.posMinting.ShouldSplitStake(
                 stakedUtxosCount: amounts.Length, 
                 amountStaked: amounts.Sum(u => u.Satoshi),
                 coinValue: amounts.First(),
                 chainHeight: ChainHeight);

            shouldStakeSplitForThe2000Coin.Should().BeFalse("coin is bigger than max value, but smaller than target value");

            var shouldStakeSplitForThe10Coin = this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Length,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Skip(1).First(),
                chainHeight: ChainHeight);

            shouldStakeSplitForThe10Coin.Should().BeFalse("because coin is below target average and max value");
        }

        [Fact]
        public void Given_A_Coin_That_Needs_Splitting_CoinstakeTx_Should_Have_SplitFactor_Non_Zero_Outputs()
        {
            var amounts = Enumerable.Repeat(100_000, 3)
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToList();

            var coinStakeContext = BuildNewCoinstakeContext();

            var expectToSplit = true;
            (long coinstakeInputValue, Transaction transaction) =
                GetCoinstakeTransaction(amounts, coinStakeContext, expectToSplit);

            var nonZeroOutputs = transaction.Outputs.Skip(1).ToList();

            nonZeroOutputs.Count().Should().Be(PosMinting.SplitFactor);
            nonZeroOutputs.Sum(t => t.Value)
                .Should().Be(coinstakeInputValue);
            nonZeroOutputs.Select(o => o.ScriptPubKey)
                .Distinct().Single().Should().Be(coinStakeContext.CoinstakeTx.Outputs[1].ScriptPubKey);
        }

        [Fact]
        public void Given_A_Coin_That_Does_Not_Need_Splitting_CoinstakeTx_Should_Have_One_Non_Zero_Outputs()
        {
            var amounts = Enumerable.Repeat(50, 2000)
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToList();

            var coinStakeContext = BuildNewCoinstakeContext();

            var expectToSplit = false;
            (long coinstakeInputValue, Transaction transaction) = 
                GetCoinstakeTransaction(amounts, coinStakeContext, expectToSplit);

            var nonZeroOutputs = transaction.Outputs.Skip(1).ToList();

            nonZeroOutputs.Count().Should().Be(1);
            nonZeroOutputs.Sum(t => t.Value)
                .Should().Be(coinstakeInputValue);
            nonZeroOutputs.Select(o => o.ScriptPubKey)
                .Distinct().Single().Should().Be(coinStakeContext.CoinstakeTx.Outputs[1].ScriptPubKey);
        }

        private (long coinstakeInputValue, Transaction transaction) GetCoinstakeTransaction(List<Money> amounts, CoinstakeContext coinStakeContext, bool expectToSplit)
        {
            long amountStaked = amounts.Sum(u => u.Satoshi);
            this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Count,
                amountStaked: amountStaked,
                coinValue: amounts.Last(),
                chainHeight: ChainHeight).Should().Be(expectToSplit);

            var reward = 1.2M * Money.COIN;
            var coinstakeInputValue = amounts.Last().Satoshi + (long)reward;

            var transaction = this.posMinting.PrepareCoinStakeTransactions(
                currentChainHeight: ChainHeight,
                coinstakeContext: coinStakeContext,
                coinstakeOutputValue: coinstakeInputValue,
                utxosCount: amounts.Count,
                amountStaked: amountStaked);
            return (coinstakeInputValue, transaction);
        }

        [Fact]
        public void Given_A_Wallet_With_Big_Coins_Then_Splitting_Should_End_When_Target_Reached()
        {
            var amounts = Enumerable.Repeat(100_000, 3)
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToList();
            var chainHeight = ChainHeight;

            //only a rough calculation to prevent infinite loop later in the test
            var targetSplitCoinValue = amounts.Sum(u => u.Satoshi) / (500 + 1) * 3;
            var maxIterations = Math.Ceiling((Math.Log(amounts.Last().Satoshi, PosMinting.SplitFactor) 
                                       - Math.Log(targetSplitCoinValue, PosMinting.SplitFactor))) + 1;

            var iterations = 0;
            while (this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Count,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Last(),
                chainHeight: ChainHeight) && iterations < maxIterations)
            {
                iterations++;
                chainHeight++;

                var transaction = GetCoinstakeTransaction(amounts);

                //replace coins in 'amounts' with the new split ones
                amounts.RemoveAt(amounts.Count - 1);
                amounts.AddRange(transaction.Outputs
                    .Select(a => a.Value).Where(a => a > 0).ToList());
            }

            iterations.Should().BeLessThan((int)maxIterations, "the loop should end when we don't need to split anymore");
            amounts.Reverse();
            amounts.Take(PosMinting.SplitFactor).All(a => a.Satoshi < targetSplitCoinValue)
                .Should().BeTrue();
        }

        private Transaction GetCoinstakeTransaction(List<Money> amounts)
        {
            var coinStakeContext = BuildNewCoinstakeContext();

            var reward = 1.2M * Money.COIN;
            var coinstakeInputValue = amounts.Last().Satoshi + (long)reward;

            var transaction = this.posMinting.PrepareCoinStakeTransactions(
                currentChainHeight: ChainHeight,
                coinstakeContext: coinStakeContext,
                coinstakeOutputValue: coinstakeInputValue,
                utxosCount: amounts.Count,
                amountStaked: amounts.Sum(u => u.Satoshi));
            return transaction;
        }

        private CoinstakeContext BuildNewCoinstakeContext()
        {
            var scriptPubKey = new Script();
            var coinStakeContext = new CoinstakeContext()
            {
                CoinstakeTx = new Transaction()
                {
                    Outputs = {
                        new TxOut(Money.Zero, new Script()),
                        new TxOut(Money.Zero, scriptPubKey)
                    }
                }
            };
            return coinStakeContext;
        }
    }
}
