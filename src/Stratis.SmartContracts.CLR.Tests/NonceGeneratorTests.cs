using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class NonceGeneratorTests
    {
        [Fact]
        public void Initial_Value_Should_Be_Correct()
        {
            var initial = 123UL;
            var nonce = new NonceGenerator(initial);
            Assert.Equal(initial, nonce.Next);
        }

        [Fact]
        public void NextValues_Should_Be_Correct()
        {
            var nonceGenerator = new NonceGenerator();
            Assert.Equal(0UL, nonceGenerator.Next);
            Assert.Equal(1UL, nonceGenerator.Next);
            Assert.Equal(2UL, nonceGenerator.Next);
            Assert.Equal(3UL, nonceGenerator.Next);
        }
    }
}