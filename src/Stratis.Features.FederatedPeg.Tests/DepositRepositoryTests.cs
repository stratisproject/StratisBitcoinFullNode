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
            Network network = CirrusNetwork.NetworksSelector.Regtest();
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
            var model = new MaturedBlockDepositsModel(new MaturedBlockInfoModel
            {
                BlockHeight = 0
            }, new List<IDeposit>
            {
                new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
            });
            var modelList = new List<MaturedBlockDepositsModel>
            {
                model
            };

            Assert.True(this.depositRepository.SaveDeposits(modelList));

            Deposit retrievedDeposit = this.depositRepository.GetDeposit(model.Deposits[0].Id);

            Assert.Equal(model.Deposits[0].Id, retrievedDeposit.Id);
            Assert.Equal(model.Deposits[0].Amount, retrievedDeposit.Amount);
            Assert.Equal(model.Deposits[0].BlockHash, retrievedDeposit.BlockHash);
            Assert.Equal(model.Deposits[0].BlockNumber, retrievedDeposit.BlockNumber);
            Assert.Equal(model.Deposits[0].TargetAddress, retrievedDeposit.TargetAddress);
        }

        [Fact]
        public void MustSyncFromZero()
        {
            var model = new MaturedBlockDepositsModel(new MaturedBlockInfoModel
            {
                BlockHeight = 1
            }, new List<IDeposit>
            {
                new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 1, 123456)
            });
            var modelList = new List<MaturedBlockDepositsModel>
            {
                model
            };

            Assert.False(this.depositRepository.SaveDeposits(modelList));

            Deposit retrievedDeposit = this.depositRepository.GetDeposit(model.Deposits[0].Id);
            Assert.Null(retrievedDeposit);
        }

        [Fact]
        public void CanSaveMultipleDepositsInOneBlock()
        {
            var modelList = new List<MaturedBlockDepositsModel>
            {
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 0
                }, new List<IDeposit>
                {
                    new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456),
                    new Deposit(1234, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
                })
            };


            Assert.True(this.depositRepository.SaveDeposits(modelList));

            foreach (IDeposit deposit in modelList[0].Deposits)
            {
                Deposit retrievedDeposit = this.depositRepository.GetDeposit(deposit.Id);

                Assert.Equal(deposit.Id, retrievedDeposit.Id);
                Assert.Equal(deposit.Amount, retrievedDeposit.Amount);
                Assert.Equal(deposit.BlockHash, retrievedDeposit.BlockHash);
                Assert.Equal(deposit.BlockNumber, retrievedDeposit.BlockNumber);
                Assert.Equal(deposit.TargetAddress, retrievedDeposit.TargetAddress);
            }
        }

        [Fact]
        public void CanSaveMultipleBlocks()
        {
            var modelList = new List<MaturedBlockDepositsModel>
            {
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 0
                }, new List<IDeposit>
                {
                    new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 1
                }, new List<IDeposit>
                {
                    new Deposit(1234, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 1, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 2
                }, new List<IDeposit>
                {
                    new Deposit(12345, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 2, 123456)
                })
            };


            Assert.True(this.depositRepository.SaveDeposits(modelList));

            foreach (MaturedBlockDepositsModel maturedBlockDeposit in modelList)
            {
                Deposit retrievedDeposit = this.depositRepository.GetDeposit(maturedBlockDeposit.Deposits[0].Id);

                Assert.Equal(maturedBlockDeposit.Deposits[0].Id, retrievedDeposit.Id);
                Assert.Equal(maturedBlockDeposit.Deposits[0].Amount, retrievedDeposit.Amount);
                Assert.Equal(maturedBlockDeposit.Deposits[0].BlockHash, retrievedDeposit.BlockHash);
                Assert.Equal(maturedBlockDeposit.Deposits[0].BlockNumber, retrievedDeposit.BlockNumber);
                Assert.Equal(maturedBlockDeposit.Deposits[0].TargetAddress, retrievedDeposit.TargetAddress);
            }
        }

        [Fact]
        public void CantSkipBlocks()
        {
            var modelList = new List<MaturedBlockDepositsModel>
            {
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 0
                }, new List<IDeposit>
                {
                    new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 1
                }, new List<IDeposit>
                {
                    new Deposit(1234, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 1, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 3
                }, new List<IDeposit>
                {
                    new Deposit(12345, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 3, 123456)
                })
            };

            Assert.False(this.depositRepository.SaveDeposits(modelList));

            Deposit retrievedDeposit = this.depositRepository.GetDeposit(modelList[0].Deposits[0].Id);
            Assert.Null(retrievedDeposit);
        }
    }
}
