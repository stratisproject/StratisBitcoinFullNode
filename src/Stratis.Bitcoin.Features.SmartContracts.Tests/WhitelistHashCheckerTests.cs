using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class WhitelistHashCheckerTests
    {
        [Fact]
        public void Should_Return_False_If_Hash_Not_In_Whitelist()
        {
            var hash = RandomUtils.GetBytes(32);

            var repository = new Mock<IWhitelistedHashesRepository>();

            repository.Setup(r => r.GetHashes()).Returns(new List<uint256>());

            var strategy = new WhitelistedHashChecker(repository.Object);

            Assert.False(strategy.CheckHashWhitelisted(hash));
        }

        [Fact]
        public void Should_Return_True_If_Hash_In_Whitelist()
        {
            var hash = RandomUtils.GetBytes(32);

            var repository = new Mock<IWhitelistedHashesRepository>();

            repository.Setup(r => r.GetHashes()).Returns(new List<uint256> { new uint256(hash) });

            var strategy = new WhitelistedHashChecker(repository.Object);

            Assert.True(strategy.CheckHashWhitelisted(hash));
        }

        [Fact]
        public void Should_Return_False_Invalid_uint256()
        {
            var repository = new Mock<IWhitelistedHashesRepository>();

            repository.Setup(r => r.GetHashes()).Returns(new List<uint256>());

            var strategy = new WhitelistedHashChecker(repository.Object);

            // uint256 must be 256 bytes wide
            var invalidUint256Bytes = new byte[] { };

            Assert.False(strategy.CheckHashWhitelisted(invalidUint256Bytes));
        }
    }
}