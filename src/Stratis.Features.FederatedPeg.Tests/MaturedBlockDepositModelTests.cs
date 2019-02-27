using FluentAssertions;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    
    public class MaturedBlockDepositModelTests
    {
        [Fact(Skip = TestingValues.SkipTests)]
        public void ShouldSerialiseAsJson()
        {
            MaturedBlockDepositsModel maturedBlockDeposits = TestingValues.GetMaturedBlockDeposits(3);
            string asJson = maturedBlockDeposits.ToString();

            var reconverted = JsonConvert.DeserializeObject<MaturedBlockDepositsModel>(asJson);

            reconverted.BlockInfo.BlockHash.Should().Be(maturedBlockDeposits.BlockInfo.BlockHash);
            reconverted.BlockInfo.BlockHeight.Should().Be(maturedBlockDeposits.BlockInfo.BlockHeight);
            reconverted.Deposits.Should().BeEquivalentTo(maturedBlockDeposits.Deposits);
        }
    }
}
