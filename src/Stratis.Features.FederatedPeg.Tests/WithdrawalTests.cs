using FluentAssertions;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class WithdrawalTests
    {
        [Fact(Skip = TestingValues.SkipTests)]
        public void ShouldSerialiseAsJson()
        {
            IWithdrawal withdrawal = TestingValues.GetWithdrawal();

            string asJson = withdrawal.ToString();
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
