using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FluentAssertions;

using NBitcoin;

using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Tests.Common;
using Xunit;
using StakingWallet = Stratis.Bitcoin.Features.Wallet.Wallet;


namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class StakeSplittingTests : PosMintingTest
    {
        private const int ChainHeight = 500000;

        private const int SplitFactor = 8;

        public StakeSplittingTests()
        {
            this.network.Consensus.Options = new PosConsensusOptions();
        }

        [Fact]
        public void Given_A_Wallet_With_Big_And_Small_Coins_Then_Big_Coins_Below_Target_Should_Get_Split()
        {
            //target value around 4700
            var amounts = new[] { 2000, 10 }
                .Concat(Enumerable.Repeat(100_000, 3))
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToArray();

            var shouldStakeSplitForThe100000Coin = this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Length,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Last(),
                splitFactor: SplitFactor,
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
                splitFactor: SplitFactor,
                chainHeight: ChainHeight);

            shouldStakeSplit.Should().BeTrue("coin is bigger than target average value");
        }

        [Fact]
        public void Given_A_Wallet_With_Big_And_Small_Coins_Then_Coins_Below_Target_Should_Get_Split()
        {
            //target value around 4700
            var amounts = new[] { 2000, 10 }
                .Concat(Enumerable.Repeat(100_000, 3))
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToArray();

            var shouldStakeSplitForThe1000Coin = this.posMinting.ShouldSplitStake(
                 stakedUtxosCount: amounts.Length, 
                 amountStaked: amounts.Sum(u => u.Satoshi),
                 coinValue: amounts.First(),
                 splitFactor: SplitFactor,
                 chainHeight: ChainHeight);

            shouldStakeSplitForThe1000Coin.Should().BeFalse("coin is bigger than max value, but smaller than ");

            var shouldStakeSplitForThe10Coin = this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Length,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Skip(1).First(),
                splitFactor: SplitFactor,
                chainHeight: ChainHeight);

            shouldStakeSplitForThe10Coin.Should().BeFalse("because coin is below target average and max value");
        }

        [Fact]
        public void Given_A_Coin_That_Needs_Splitting_CoinstakeTx_Should_Have_SplitFactorPlus1_Outputs()
        {
            var amounts = Enumerable.Repeat(100_000, 3)
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToList();

            this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Count,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Last(),
                splitFactor: SplitFactor,
                chainHeight: ChainHeight).Should().BeTrue();

            //TODO: finish test where we make sure we have SplitFactor outputs and that their sum is expected reward + coin
            //var scriptPubKey = new Script();
            //var coinStakeContext = new CoinstakeContext()
            //   {
            //        CoinstakeTx = new Transaction() { Outputs =
            //        {
            //            new TxOut(Money.Zero, new Script()),
            //            new TxOut(Money.Zero, scriptPubKey)
            //        }}
            //   }

            //this.posMinting.PrepareCoinStakeTransaction(
            //    currentChainHeight: ChainHeight,
            //    coinstakeContext: coinStakeContext,
            //    new CoinstakeWorkerResult(){new UtxoStakeDescription(){}}, 
                
            //    )
        }

        [Fact]
        public void Given_A_Wallet_With_Big_Coins_Then_Splitting_Should_End_When_Target_Reached()
        {
            //target value around 4700
            var amounts = Enumerable.Repeat(100_000, 3)
                .Select(a => new Money(a, MoneyUnit.BTC))
                .ToList();

            while (this.posMinting.ShouldSplitStake(
                stakedUtxosCount: amounts.Count,
                amountStaked: amounts.Sum(u => u.Satoshi),
                coinValue: amounts.Last(),
                splitFactor: SplitFactor,
                chainHeight: ChainHeight))
            {
                //amounts.RemoveAt(amounts.Count-1);
                //todo: make the last utxo a winner on each transaction and make sure this loops ends when target is reached
                break;
            }
        }
    }
}
