using FluentAssertions;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class MaturedBlockRequestModelTests
    {
        [Fact]
        public void ShouldSerialiseAsJson()
        {
            var maturedBlockDeposits = new MaturedBlockRequestModel(TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());
            var asJson = maturedBlockDeposits.ToString();

            var reconverted = JsonConvert.DeserializeObject<MaturedBlockRequestModel>(asJson);

            reconverted.BlockHeight.Should().Be(maturedBlockDeposits.BlockHeight);
            reconverted.MaxBlocksToSend.Should().Be(maturedBlockDeposits.MaxBlocksToSend);
        }
    }
}