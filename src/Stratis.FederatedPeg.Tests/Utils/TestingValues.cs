using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

namespace Stratis.FederatedPeg.Tests.Utils
{
    using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

    public static class TestingValues
    {
        private static readonly Random Random = new Random(DateTime.Now.Millisecond);

        public static uint256 GetUint256()
        {
            var buffer = new byte[256 / 8];
            Random.NextBytes(buffer);
            return new uint256(buffer);
        }

        public static int GetPositiveInt(int minValue = 0)
        {
            return Random.Next(minValue, int.MaxValue);
        }

        public static Money GetMoney(int minValue = 1, bool wholeCoins = false)
        {
            return wholeCoins ? new Money(GetPositiveInt(minValue)) : new Money(GetPositiveInt(minValue), MoneyUnit.BTC);
        }

        public static string GetString(int length = 30)
        {
            const string allowed = "abcdefghijklmnopqrstuvwxyz0123456789";
            var result = new string(Enumerable.Repeat("_", length)
                .Select(_ => allowed[Random.Next(0, allowed.Length)])
                .ToArray());
            return result;
        }

        public static HashHeightPair GetHashHeightPair(uint256 blockHash = null, int blockHeight = -1)
        {
            blockHash = blockHash ?? GetUint256();
            if (blockHeight == -1) blockHeight = GetPositiveInt();
            var hashHeightPair = new HashHeightPair(blockHash, blockHeight);
            return hashHeightPair;
        }

        public static IDeposit GetDeposit(HashHeightPair hashHeightPair = null)
        {
            hashHeightPair = hashHeightPair ?? GetHashHeightPair();
            var depositId = GetUint256();
            var depositAmount = GetMoney();
            var targetAddress = GetString();

            return new Deposit(depositId, depositAmount, targetAddress, hashHeightPair.Height, hashHeightPair.Hash);
        }

        public static IMaturedBlockDeposits GetMaturedBlockDeposits(int depositCount = 0)
        {
            var hashHeightPair = GetHashHeightPair();
            var deposits = Enumerable.Range(0, depositCount).Select(_ => GetDeposit(hashHeightPair));

            var maturedBlockDeposits = new MaturedBlockDepositsModel(
                new MaturedBlockModel() { BlockHash = hashHeightPair.Hash, BlockHeight = hashHeightPair.Height },
                deposits.ToList());
            return maturedBlockDeposits;
        }

        public static IWithdrawal GetWithdrawal(HashHeightPair hashHeightPair = null)
        {
            hashHeightPair = hashHeightPair ?? GetHashHeightPair();
            var depositId = GetUint256();
            var id = GetUint256();
            var amount = GetMoney();
            var targetAddress = GetString();

            return new Withdrawal(depositId, id, amount, targetAddress, hashHeightPair.Height, hashHeightPair.Hash);
        }


        public static IReadOnlyList<IWithdrawal> GetWithdrawals(int count)
        {
            return Enumerable.Range(0, count).Select(_ => GetWithdrawal()).ToList().AsReadOnly();
        }
    }
}
