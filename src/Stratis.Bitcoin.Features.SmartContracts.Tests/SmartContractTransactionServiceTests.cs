using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractTransactionServiceTests
    {
        private readonly Network network;
        private readonly Mock<IWalletManager> walletManager = new Mock<IWalletManager>();
        private readonly Mock<IWalletTransactionHandler> walletTransactionHandler;
        private readonly Mock<IMethodParameterStringSerializer> stringSerializer;
        private readonly Mock<ICallDataSerializer> callDataSerializer;
        private readonly Mock<IAddressGenerator> addressGenerator;

        public SmartContractTransactionServiceTests()
        {
            this.network = new SmartContractsRegTest();
            this.walletManager = new Mock<IWalletManager>();
            this.walletTransactionHandler = new Mock<IWalletTransactionHandler>();
            this.stringSerializer = new Mock<IMethodParameterStringSerializer>();
            this.callDataSerializer = new Mock<ICallDataSerializer>();
            this.addressGenerator = new Mock<IAddressGenerator>();
        }


        [Fact]
        public void CanChooseInputsForCall()
        {
            const int utxoIndex = 0;
            uint256 utxoId = uint256.Zero;
            uint256 utxoIdUnused = uint256.One;
            string senderAddress = uint160.Zero.ToBase58Address(this.network);
            string contractAddress = uint160.One.ToBase58Address(this.network);

            var request = new BuildCallContractTransactionRequest
            {
                Amount = "0",
                AccountName = "account 0",
                ContractAddress = contractAddress,
                FeeAmount = "0.01",
                GasLimit = 100_000,
                GasPrice = 100,
                MethodName = "TestMethod",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
                Outpoints = new List<OutpointRequestModel>
                {
                    new OutpointRequestModel
                    {
                        Index = utxoIndex,
                        TransactionId = utxoId.ToString()
                    }, 
                }
            };

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance
                {
                    Address = senderAddress,
                    AmountConfirmed = new Money(100, MoneyUnit.BTC)
                });

            this.walletManager.Setup(x => x.GetSpendableTransactionsInWallet(It.IsAny<string>(), 0))
                .Returns(new List<UnspentOutputReference>
                {
                    new UnspentOutputReference
                    {
                        Address = new HdAddress
                        {
                            Address = senderAddress
                        },
                        Transaction = new TransactionData
                        {
                            Id = utxoId,
                            Index = utxoIndex,
                        }
                    }, new UnspentOutputReference
                    {
                        Address = new HdAddress
                        {
                            Address = senderAddress
                        },
                        Transaction = new TransactionData
                        {
                            Id = utxoIdUnused,
                            Index = utxoIndex,
                        }
                    }
                });

            this.walletManager.Setup(x => x.GetWallet(request.WalletName))
                .Returns(new Features.Wallet.Wallet
                {
                    AccountsRoot = new List<AccountRoot>
                    {
                        new AccountRoot
                        {
                            Accounts = new List<HdAccount>
                            {
                                new HdAccount
                                {
                                    ExternalAddresses = new List<HdAddress>
                                    {
                                        new HdAddress
                                        {
                                            Address = senderAddress
                                        }
                                    },
                                    Name = request.AccountName
                                }
                            }
                        }
                    }
                });

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object);

            BuildCallContractTransactionResponse result = service.BuildCallTx(request);

            this.walletTransactionHandler.Verify(x=>x.BuildTransaction(It.Is<TransactionBuildContext>(y =>y.SelectedInputs.Count == 1)));
        }

        [Fact]
        public void ChoosingInvalidInputFails()
        {
            const int utxoIndex = 0;
            uint256 utxoId = uint256.Zero;
            uint256 utxoIdUnused = uint256.One;
            string senderAddress = uint160.Zero.ToBase58Address(this.network);
            string contractAddress = uint160.One.ToBase58Address(this.network);

            var request = new BuildCallContractTransactionRequest
            {
                Amount = "0",
                AccountName = "account 0",
                ContractAddress = contractAddress,
                FeeAmount = "0.01",
                GasLimit = 100_000,
                GasPrice = 100,
                MethodName = "TestMethod",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
                Outpoints = new List<OutpointRequestModel>
                {
                    new OutpointRequestModel
                    {
                        Index = utxoIndex,
                        TransactionId = new uint256(64).ToString() // A tx we don't have.
                    },
                }
            };

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance
                {
                    Address = senderAddress,
                    AmountConfirmed = new Money(100, MoneyUnit.BTC)
                });

            this.walletManager.Setup(x => x.GetSpendableTransactionsInWallet(It.IsAny<string>(), 0))
                .Returns(new List<UnspentOutputReference>
                {
                    new UnspentOutputReference
                    {
                        Address = new HdAddress
                        {
                            Address = senderAddress
                        },
                        Transaction = new TransactionData
                        {
                            Id = utxoId,
                            Index = utxoIndex,
                        }
                    }, new UnspentOutputReference
                    {
                        Address = new HdAddress
                        {
                            Address = senderAddress
                        },
                        Transaction = new TransactionData
                        {
                            Id = utxoIdUnused,
                            Index = utxoIndex,
                        }
                    }
                });

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object);

            BuildCallContractTransactionResponse result = service.BuildCallTx(request);
            Assert.False(result.Success);
            Assert.StartsWith("Invalid list of request outpoints", result.Message);
        }

        [Fact]
        public void CanChooseInputsForCreate()
        {
            const int utxoIndex = 0;
            uint256 utxoId = uint256.Zero;
            uint256 utxoIdUnused = uint256.One;
            string senderAddress = uint160.Zero.ToBase58Address(this.network);
            string contractAddress = uint160.One.ToBase58Address(this.network);

            var request = new BuildCreateContractTransactionRequest
            {
                Amount = "0",
                AccountName = "account 0",
                ContractCode = "AB1234",
                FeeAmount = "0.01",
                GasLimit = 100_000,
                GasPrice = 100,
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
                Outpoints = new List<OutpointRequestModel>
                {
                    new OutpointRequestModel
                    {
                        Index = utxoIndex,
                        TransactionId = utxoId.ToString()
                    },
                }
            };

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance
                {
                    Address = senderAddress,
                    AmountConfirmed = new Money(100, MoneyUnit.BTC)
                });

            this.walletManager.Setup(x => x.GetSpendableTransactionsInWallet(It.IsAny<string>(), 0))
                .Returns(new List<UnspentOutputReference>
                {
                    new UnspentOutputReference
                    {
                        Address = new HdAddress
                        {
                            Address = senderAddress
                        },
                        Transaction = new TransactionData
                        {
                            Id = utxoId,
                            Index = utxoIndex,
                        }
                    }, new UnspentOutputReference
                    {
                        Address = new HdAddress
                        {
                            Address = senderAddress
                        },
                        Transaction = new TransactionData
                        {
                            Id = utxoIdUnused,
                            Index = utxoIndex,
                        }
                    }
                });

            this.walletManager.Setup(x => x.GetWallet(request.WalletName))
                .Returns(new Features.Wallet.Wallet
                {
                    AccountsRoot = new List<AccountRoot>
                    {
                        new AccountRoot
                        {
                            Accounts = new List<HdAccount>
                            {
                                new HdAccount
                                {
                                    ExternalAddresses = new List<HdAddress>
                                    {
                                        new HdAddress
                                        {
                                            Address = senderAddress
                                        }
                                    },
                                    Name = request.AccountName
                                }
                            }
                        }
                    }
                });

            this.callDataSerializer.Setup(x => x.Deserialize(It.IsAny<byte[]>()))
                .Returns(Result.Ok(new ContractTxData(1, 100, (Gas) 100_000, new byte[0])));

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object);

            BuildCreateContractTransactionResponse result = service.BuildCreateTx(request);

            this.walletTransactionHandler.Verify(x => x.BuildTransaction(It.Is<TransactionBuildContext>(y => y.SelectedInputs.Count == 1)));
        }

    }
}
