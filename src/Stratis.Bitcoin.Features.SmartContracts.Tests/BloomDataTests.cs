using System;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class BloomDataTests
    {
        [Fact]
        public void TestEquality()
        {
            // Inited, both equal
            var bloom1 = new BloomData();
            var bloom2 = new BloomData();
            Assert.Equal(bloom1, bloom2);

            // Which is the same as initing with all 0s
            byte[] zeroedData = new byte[256];
            bloom2 = new BloomData(zeroedData);
            Assert.Equal(bloom1, bloom2);

            // Different value is not equal.
            byte[] differentData = new byte[256];
            differentData[2] = 16;
            bloom2 = new BloomData(differentData);
            Assert.NotEqual(bloom1, bloom2);
        }

        [Fact]
        public void LengthMustBeCorrect()
        {
            new BloomData(new byte[256]); // no exception
            Assert.Throws<ArgumentException>(() => new BloomData(new byte[0]));
            Assert.Throws<ArgumentException>(() => new BloomData(new byte[257]));
            Assert.Throws<ArgumentException>(() => new BloomData(null));
        }
    }
}
