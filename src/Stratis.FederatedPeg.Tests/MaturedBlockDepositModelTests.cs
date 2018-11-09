using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class MaturedBlockDepositModelTests
    {
        public static IMaturedBlockDeposits PrepareMaturedBlockDeposits()
        {
            var blockHash = TestingValues.GetUint256();
            var blockHeight = TestingValues.GetPositiveInt();
            var depositId = TestingValues.GetUint256();
            var depositAmount = TestingValues.GetMoney();
            var targetAddress = TestingValues.GetString();

            var maturedBlockDeposits = new MaturedBlockDepositsModel(
                new MaturedBlockModel() { BlockHash = blockHash, BlockHeight = blockHeight },
                new List<IDeposit>() { new Deposit(depositId, depositAmount, targetAddress, blockHeight, blockHash) });
            return maturedBlockDeposits;
        }

        [Fact]
        public void ShouldSerialiseAsJson()
        {
            var maturedBlockDeposits = PrepareMaturedBlockDeposits();
            var asJson = maturedBlockDeposits.ToString();

            var reconverted = JsonConvert.DeserializeObject<MaturedBlockDepositsModel>(asJson);

            reconverted.Block.BlockHash.Should().Be(maturedBlockDeposits.Block.BlockHash);
            reconverted.Block.BlockHeight.Should().Be(maturedBlockDeposits.Block.BlockHeight);
            reconverted.Deposits.Should().BeEquivalentTo(maturedBlockDeposits.Deposits);
        }
    }
}
