using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;

using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class WithdrawalTests
    {
        [Fact]
        public void ShouldSerialiseAsJson()
        {
            var withdrawal = TestingValues.GetWithdrawal();

            var asJson = withdrawal.ToString();
            var reconverted = JsonConvert.DeserializeObject<Withdrawal>(asJson);

            reconverted.BlockHash.Should().Be(withdrawal.BlockHash);
            reconverted.Amount.Satoshi.Should().Be(withdrawal.Amount.Satoshi);
            reconverted.BlockNumber.Should().Be(withdrawal.BlockNumber);
            reconverted.Id.Should().Be(withdrawal.Id);
            reconverted.DepositId.Should().Be(withdrawal.DepositId);
            reconverted.TargetAddress.Should().Be(withdrawal.TargetAddress);
        }
    }
}
