using FluentAssertions;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class MaturedBlockRequestModelTests
    {
        [Fact(Skip = TestingValues.SkipTests)]
        public void ShouldSerialiseAsJson()
        {
            var maturedBlockDeposits = new MaturedBlockRequestModel(TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());
            string asJson = maturedBlockDeposits.ToString();

            MaturedBlockRequestModel reconverted = JsonConvert.DeserializeObject<MaturedBlockRequestModel>(asJson);

            reconverted.BlockHeight.Should().Be(maturedBlockDeposits.BlockHeight);
            reconverted.MaxBlocksToSend.Should().Be(maturedBlockDeposits.MaxBlocksToSend);
        }
    }
}