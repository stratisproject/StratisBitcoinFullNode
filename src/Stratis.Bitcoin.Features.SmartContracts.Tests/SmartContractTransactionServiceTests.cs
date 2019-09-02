using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.State;
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
        private readonly Mock<IStateRepositoryRoot> stateRepository;

        public SmartContractTransactionServiceTests()
        {
            this.network = new SmartContractsRegTest();
            this.walletManager = new Mock<IWalletManager>();
            this.walletTransactionHandler = new Mock<IWalletTransactionHandler>();
            this.stringSerializer = new Mock<IMethodParameterStringSerializer>();
            this.callDataSerializer = new Mock<ICallDataSerializer>();
            this.addressGenerator = new Mock<IAddressGenerator>();
            this.stateRepository = new Mock<IStateRepositoryRoot>();
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
                Outpoints = new List<OutpointRequest>
                {
                    new OutpointRequest
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
                this.addressGenerator.Object,
                this.stateRepository.Object);

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
                Outpoints = new List<OutpointRequest>
                {
                    new OutpointRequest
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
                this.addressGenerator.Object,
                this.stateRepository.Object);

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
                Outpoints = new List<OutpointRequest>
                {
                    new OutpointRequest
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
                this.addressGenerator.Object,
                this.stateRepository.Object);

            BuildCreateContractTransactionResponse result = service.BuildCreateTx(request);

            this.walletTransactionHandler.Verify(x => x.BuildTransaction(It.Is<TransactionBuildContext>(y => y.SelectedInputs.Count == 1)));
        }

        [Fact]
        public void BuildTransferContext_SenderNotInWallet_Fails()
        {
            string senderAddress = uint160.Zero.ToBase58Address(this.network);

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object,
                this.stateRepository.Object);

            var request = new BuildContractTransactionRequest
            {
                AccountName = "account 0",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
            };

            // Create a wallet but without the sender address
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
                                    ExternalAddresses = new List<HdAddress>(),
                                    Name = request.AccountName
                                }
                            }
                        }
                    }
                });

            BuildContractTransactionResult result = service.BuildTx(request);

            Assert.Equal(SmartContractTransactionService.SenderNotInWalletError, result.Error);
            Assert.NotNull(result.Message);
            Assert.Null(result.Response);
        }

        [Fact]
        public void BuildTransferContext_AccountNotInWallet_Fails()
        {
            string senderAddress = uint160.Zero.ToBase58Address(this.network);

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object,
                this.stateRepository.Object);

            var request = new BuildContractTransactionRequest
            {
                AccountName = "account 0",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
            };

            // Create a wallet but without the correct account name
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
                                    ExternalAddresses = new List<HdAddress>(),
                                    Name = "account 1"
                                }
                            }
                        }
                    }
                });

            BuildContractTransactionResult result = service.BuildTx(request);

            Assert.Equal(SmartContractTransactionService.AccountNotInWalletError, result.Error);
            Assert.NotNull(result.Message);
            Assert.Null(result.Response);
        }

        [Fact]
        public void BuildTransferContext_SenderHasNoBalance_Fails()
        {
            string senderAddress = uint160.Zero.ToBase58Address(this.network);

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object,
                this.stateRepository.Object);

            var request = new BuildContractTransactionRequest
            {
                AccountName = "account 0",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
            };

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
                                    Name = request.AccountName,                                    
                                }
                            }
                        }
                    }
                });

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance { Address = request.Sender, AmountConfirmed = 0, AmountUnconfirmed = 0 });

            BuildContractTransactionResult result = service.BuildTx(request);

            Assert.Equal(SmartContractTransactionService.InsufficientBalanceError, result.Error);
            Assert.NotNull(result.Message);
            Assert.Null(result.Response);
        }

        [Fact]
        public void BuildTransferContext_RecipientIsKnownContract_Fails()
        {
            const int utxoIndex = 0;
            uint256 utxoId = uint256.Zero;
            uint256 utxoIdUnused = uint256.One;
            string senderAddress = uint160.Zero.ToBase58Address(this.network);
            string recipientAddress = uint160.One.ToBase58Address(this.network);

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object,
                this.stateRepository.Object);

            var request = new BuildContractTransactionRequest
            {
                AccountName = "account 0",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
                Recipients = new List<RecipientModel>
                {
                    new RecipientModel { Amount = "1", DestinationAddress = recipientAddress}
                }
            };

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
                                    Name = request.AccountName,
                                }
                            }
                        }
                    }
                });

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance { Address = request.Sender, AmountConfirmed = 10, AmountUnconfirmed = 0 });

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

            this.stateRepository.Setup(s => s.IsExist(It.IsAny<uint160>())).Returns(true);

            BuildContractTransactionResult result = service.BuildTx(request);

            Assert.Equal(SmartContractTransactionService.TransferFundsToContractError, result.Error);
            Assert.NotNull(result.Message);
            Assert.Null(result.Response);
        }

        [Fact]
        public void BuildTransferContextCorrectly()
        {
            const int utxoIndex = 0;
            uint256 utxoId = uint256.Zero;
            uint256 utxoIdUnused = uint256.One;
            string senderAddress = uint160.Zero.ToBase58Address(this.network);
            string recipientAddress = uint160.One.ToBase58Address(this.network);
            string changeAddress = new uint160(2).ToBase58Address(this.network);

            var amount = 1234.567M;

            var request = new BuildContractTransactionRequest
            {
                AccountName = "account 0",
                FeeAmount = "0.01",
                WalletName = "wallet",
                Password = "password",
                Sender = senderAddress,
                ShuffleOutputs = true,
                AllowUnconfirmed = true,
                ChangeAddress = changeAddress,
                Recipients = new List<RecipientModel>
                {
                    new RecipientModel { Amount = amount.ToString(), DestinationAddress = recipientAddress}
                }
            };

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object,
                this.stateRepository.Object);

            var senderHdAddress = new HdAddress { Address = senderAddress };

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
                                        senderHdAddress 
                                    },
                                    Name = request.AccountName,
                                }
                            }
                        }
                    }
                });

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance { Address = request.Sender, AmountConfirmed = Money.FromUnit(amount, MoneyUnit.BTC), AmountUnconfirmed = 0 });

            var outputs = new List<UnspentOutputReference>
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
                };

            this.walletManager.Setup(x => x.GetSpendableTransactionsInWallet(It.IsAny<string>(), 0)).Returns(outputs);

            this.walletTransactionHandler.Setup(x => x.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(new Transaction());

            BuildContractTransactionResult result = service.BuildTx(request);

            // Check that the transaction builder is invoked, and that we:
            // - Ignore shuffleOutputs,
            // - Set inputs from sender
            // - Set recipients,
            // - Set change to sender
            this.walletTransactionHandler.Verify(w => w.BuildTransaction(It.Is<TransactionBuildContext>(context =>
                context.AllowOtherInputs == false &&
                context.Shuffle == false &&
                context.SelectedInputs.All(i => outputs.Select(o => o.Transaction.Id).Contains(i.Hash)) &&
                context.Recipients.Single().Amount == Money.FromUnit(amount, MoneyUnit.BTC) &&
                context.ChangeAddress == senderHdAddress
            )));
        }

        [Fact]
        public void BuildFeeEstimationContextCorrectly()
        {
            const int utxoIndex = 0;
            uint256 utxoId = uint256.Zero;
            uint256 utxoIdUnused = uint256.One;
            string senderAddress = uint160.Zero.ToBase58Address(this.network);
            string recipientAddress = uint160.One.ToBase58Address(this.network);
            string changeAddress = new uint160(2).ToBase58Address(this.network);

            var amount = 1234.567M;

            var request = new ScTxFeeEstimateRequest
            {
                AccountName = "account 0",
                WalletName = "wallet",
                Sender = senderAddress,
                ShuffleOutputs = true,
                AllowUnconfirmed = true,
                ChangeAddress = changeAddress,
                Recipients = new List<RecipientModel>
                {
                    new RecipientModel { Amount = amount.ToString(), DestinationAddress = recipientAddress}
                },
                FeeType = "medium"
            };

            SmartContractTransactionService service = new SmartContractTransactionService(
                this.network,
                this.walletManager.Object,
                this.walletTransactionHandler.Object,
                this.stringSerializer.Object,
                this.callDataSerializer.Object,
                this.addressGenerator.Object,
                this.stateRepository.Object);

            var senderHdAddress = new HdAddress { Address = senderAddress };

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
                                        senderHdAddress
                                    },
                                    Name = request.AccountName,
                                }
                            }
                        }
                    }
                });

            this.walletManager.Setup(x => x.GetAddressBalance(request.Sender))
                .Returns(new AddressBalance { Address = request.Sender, AmountConfirmed = Money.FromUnit(amount, MoneyUnit.BTC), AmountUnconfirmed = 0 });

            var outputs = new List<UnspentOutputReference>
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
                };

            this.walletManager.Setup(x => x.GetSpendableTransactionsInWallet(It.IsAny<string>(), 0)).Returns(outputs);

            this.walletTransactionHandler.Setup(x => x.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(new Transaction());

            EstimateFeeResult result = service.EstimateFee(request);

            // Check that the transaction builder is invoked, and that we:
            // - Ignore shuffleOutputs,
            // - Set inputs from sender
            // - Set recipients,
            // - Set change to sender
            // - Set the fee type correctly
            // - Set sign to false
            // - Set transaction fee to null
            this.walletTransactionHandler.Verify(w => w.EstimateFee(It.Is<TransactionBuildContext>(context =>
                context.AllowOtherInputs == false &&
                context.Shuffle == false &&
                context.SelectedInputs.All(i => outputs.Select(o => o.Transaction.Id).Contains(i.Hash)) &&
                context.Recipients.Single().Amount == Money.FromUnit(amount, MoneyUnit.BTC) &&
                context.ChangeAddress == senderHdAddress &&
                context.Sign == false &&
                context.TransactionFee == null &&
                context.FeeType == FeeType.Medium
            )));
        }
    }
}
