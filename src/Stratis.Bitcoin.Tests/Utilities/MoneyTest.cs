using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class MoneyTest
    {
        [Fact]
        public void MoneyTryParseCanParseScientificNotation()
        {
            Assert.True(Money.TryParse("1e8", out Money value));
            Assert.Equal(new Money(100000000m, MoneyUnit.BTC), value);
        }
    }
}
