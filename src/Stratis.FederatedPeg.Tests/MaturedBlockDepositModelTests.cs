using System.Linq;
using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;

using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    
    public class MaturedBlockDepositModelTests
    {
        [Fact]
        public void ShouldSerialiseAsJson()
        {
            var maturedBlockDeposits = TestingValues.GetMaturedBlockDeposits(3);
            var asJson = maturedBlockDeposits.ToString();

            var reconverted = JsonConvert.DeserializeObject<MaturedBlockDepositsModel>(asJson);

            reconverted.Block.BlockHash.Should().Be(maturedBlockDeposits.Block.BlockHash);
            reconverted.Block.BlockHeight.Should().Be(maturedBlockDeposits.Block.BlockHeight);
            reconverted.Deposits.Should().BeEquivalentTo(maturedBlockDeposits.Deposits);
        }
    }
}
