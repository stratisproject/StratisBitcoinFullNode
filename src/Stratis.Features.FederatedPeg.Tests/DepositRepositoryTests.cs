using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Sidechains.Networks.CirrusV2;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class DepositRepositoryTests
    {
        private readonly DepositRepository depositRepository;

        public DepositRepositoryTests()
        {
            var network = new CirrusRegTest();
            var dbreezeSerializer = new DBreezeSerializer(network);
            DataFolder dataFolder = TestBase.CreateDataFolder(this);
            Mock<IFederationGatewaySettings> settings = new Mock<IFederationGatewaySettings>();
            settings.Setup(x => x.MultiSigAddress)
                .Returns(new BitcoinPubKeyAddress(new KeyId(), network));

            this.depositRepository = new DepositRepository(dataFolder, settings.Object, dbreezeSerializer);
        }

        [Fact]
        public void StoreAndRetrieveDeposit()
        {
            var model = new MaturedBlockDepositsModel(null, new List<IDeposit>
            {
                new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 26, 123456)
            });
            var modelList = new List<MaturedBlockDepositsModel>
            {
                model
            };

            this.depositRepository.SaveDeposits(modelList);

            Deposit retrievedDeposit = this.depositRepository.GetDeposit(model.Deposits[0].Id);

            Assert.Equal(model.Deposits[0].Id, retrievedDeposit.Id);
            Assert.Equal(model.Deposits[0].Amount, retrievedDeposit.Amount);
            Assert.Equal(model.Deposits[0].BlockHash, retrievedDeposit.BlockHash);
            Assert.Equal(model.Deposits[0].BlockNumber, retrievedDeposit.BlockNumber);
            Assert.Equal(model.Deposits[0].TargetAddress, retrievedDeposit.TargetAddress);
        }

        [Fact]
        public void DepositsSavedWhenStoredTwice()
        {
            var model = new MaturedBlockDepositsModel(null, new List<IDeposit>
            {
                new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 26, 123456)
            });
            var modelList = new List<MaturedBlockDepositsModel>
            {
                model
            };

            this.depositRepository.SaveDeposits(modelList);

            Deposit retrievedDeposit = this.depositRepository.GetDeposit(model.Deposits[0].Id);

            Assert.Equal(model.Deposits[0].Id, retrievedDeposit.Id);
            Assert.Equal(model.Deposits[0].Amount, retrievedDeposit.Amount);
            Assert.Equal(model.Deposits[0].BlockHash, retrievedDeposit.BlockHash);
            Assert.Equal(model.Deposits[0].BlockNumber, retrievedDeposit.BlockNumber);
            Assert.Equal(model.Deposits[0].TargetAddress, retrievedDeposit.TargetAddress);

            // Storing the same deposits twice isn't problematic - the API may query for this.

            this.depositRepository.SaveDeposits(modelList);

            retrievedDeposit = this.depositRepository.GetDeposit(model.Deposits[0].Id);

            Assert.Equal(model.Deposits[0].Id, retrievedDeposit.Id);
            Assert.Equal(model.Deposits[0].Amount, retrievedDeposit.Amount);
            Assert.Equal(model.Deposits[0].BlockHash, retrievedDeposit.BlockHash);
            Assert.Equal(model.Deposits[0].BlockNumber, retrievedDeposit.BlockNumber);
            Assert.Equal(model.Deposits[0].TargetAddress, retrievedDeposit.TargetAddress);
        }


    }
}
