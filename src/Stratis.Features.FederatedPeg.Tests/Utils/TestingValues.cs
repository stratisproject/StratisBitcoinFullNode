using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Tests.Utils
{
    public static class TestingValues
    {

        /// <summary>
        /// Utility to run tests when developing. Set to null to run sidechains tests.
        /// </summary>
        public const string SkipTests = "Currently skipping all sidechains tests until they are stable. Make TestingValues.SkipTests null to prevent skipping.";

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
            string result = new string(Enumerable.Repeat("_", length)
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
            uint256 depositId = GetUint256();
            Money depositAmount = GetMoney();
            string targetAddress = GetString();

            return new Deposit(depositId, depositAmount, targetAddress, hashHeightPair.Height, hashHeightPair.Hash);
        }

        public static MaturedBlockDepositsModel GetMaturedBlockDeposits(int depositCount = 0, HashHeightPair fixedHashHeight = null)
        {
            HashHeightPair hashHeightPair = fixedHashHeight ?? GetHashHeightPair();
            IEnumerable<IDeposit> deposits = Enumerable.Range(0, depositCount).Select(_ => GetDeposit(hashHeightPair));

            var maturedBlockDeposits = new MaturedBlockDepositsModel(
                new MaturedBlockInfoModel() { BlockHash = hashHeightPair.Hash, BlockHeight = hashHeightPair.Height },
                deposits.ToList());
            return maturedBlockDeposits;
        }

        public static IWithdrawal GetWithdrawal(HashHeightPair hashHeightPair = null)
        {
            hashHeightPair = hashHeightPair ?? GetHashHeightPair();
            uint256 depositId = GetUint256();
            uint256 id = GetUint256();
            Money amount = GetMoney();
            string targetAddress = GetString();

            return new Withdrawal(depositId, id, amount, targetAddress, hashHeightPair.Height, hashHeightPair.Hash);
        }


        public static IReadOnlyList<IWithdrawal> GetWithdrawals(int count)
        {
            return Enumerable.Range(0, count).Select(_ => GetWithdrawal()).ToList().AsReadOnly();
        }
    }
}
