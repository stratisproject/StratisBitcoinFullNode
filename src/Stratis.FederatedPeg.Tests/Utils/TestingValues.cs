using System;
using System.Linq;
using NBitcoin;

namespace Stratis.FederatedPeg.Tests.Utils
{
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
    }
}
