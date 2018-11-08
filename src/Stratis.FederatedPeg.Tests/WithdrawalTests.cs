using FluentAssertions;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class WithdrawalTests
    {
        public static IWithdrawal PrepareWithdrawal()
        {
            var depositId = TestingValues.GetUint256();
            var id = TestingValues.GetUint256();
            var amount = TestingValues.GetMoney();
            var targetAddress = TestingValues.GetString();
            var blockNumber = TestingValues.GetPositiveInt();
            var blockHash = TestingValues.GetUint256();

            return new Withdrawal(depositId, id, amount, targetAddress, blockNumber, blockHash);
        }

        [Fact]
        public void ShouldSerialiseAsJson()
        {
            var withdrawal = PrepareWithdrawal();

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
