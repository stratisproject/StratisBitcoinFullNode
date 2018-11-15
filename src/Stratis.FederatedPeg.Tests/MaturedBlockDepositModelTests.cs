using System.Linq;
using FluentAssertions;
using NBitcoin;
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
        public static IDeposit PrepareDeposit(uint256 blockHash = null, int blockHeight = -1)
        {
            blockHash = blockHash ?? TestingValues.GetUint256();
            if (blockHeight == -1) blockHeight = TestingValues.GetPositiveInt();
            var depositId = TestingValues.GetUint256();
            var depositAmount = TestingValues.GetMoney();
            var targetAddress = TestingValues.GetString();

            return new Deposit(depositId, depositAmount, targetAddress, blockHeight, blockHash);
        }

        public static IMaturedBlockDeposits PrepareMaturedBlockDeposits(int depositCount = 0)
        {
            var blockHash = TestingValues.GetUint256();
            var blockHeight = TestingValues.GetPositiveInt();
            var deposits = Enumerable.Range(0, depositCount).Select(_ => PrepareDeposit(blockHash, blockHeight));

            var maturedBlockDeposits = new MaturedBlockDepositsModel(
                new MaturedBlockModel() { BlockHash = blockHash, BlockHeight = blockHeight },
                deposits.ToList());
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
