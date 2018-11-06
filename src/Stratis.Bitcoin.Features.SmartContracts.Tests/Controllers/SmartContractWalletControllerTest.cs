using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Controllers
{
    public class SmartContractWalletControllerTest
    {
        private readonly Mock<IAddressGenerator> addressGenerator;
        private readonly Mock<IBroadcasterManager> broadcasterManager;
        private readonly Mock<ICallDataSerializer> callDataSerializer;
        private readonly Mock<IConnectionManager> connectionManager;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Network network;
        private readonly Mock<IWalletManager> walletManager;

        public SmartContractWalletControllerTest()
        {
            this.addressGenerator = new Mock<IAddressGenerator>();
            this.broadcasterManager = new Mock<IBroadcasterManager>();
            this.callDataSerializer = new Mock<ICallDataSerializer>();
            this.connectionManager = new Mock<IConnectionManager>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.network = new SmartContractsRegTest();
            this.walletManager = new Mock<IWalletManager>();
        }

        [Fact]
        public void GetHistoryWithValidModelWithoutTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice;
            int vmVersion = 1;
            Gas gasLimit = (Gas)(SmartContractFormatRule.GasLimitMaximum / 2);
            var contractTxData = new ContractTxData(vmVersion, gasPrice, gasLimit,new byte[]{0, 1, 2, 3});
            var callDataSerializer = new CallDataSerializer(new ContractPrimitiveSerializer(new SmartContractsRegTest()));
            var contractCreateScript = new Script(callDataSerializer.Serialize(contractTxData));

            string walletName = "myWallet";
            HdAddress address = WalletTestsHelpers.CreateAddress();
            TransactionData normalTransaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            TransactionData createTransaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            createTransaction.SpendingDetails = new SpendingDetails
            {
                BlockHeight = 100,
                CreationTime = DateTimeOffset.Now,
                TransactionId = uint256.One,
                Payments = new List<PaymentDetails>
                {
                    new PaymentDetails
                    {
                        Amount = new Money(100000),
                        DestinationScriptPubKey = contractCreateScript
                    }
                }
            };

            address.Transactions.Add(normalTransaction);
            address.Transactions.Add(createTransaction);

            var addresses = new List<HdAddress> { address };
            Features.Wallet.Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            var account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };
            this.walletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            this.walletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            this.addressGenerator.Setup(x => x.GenerateAddress(It.IsAny<uint256>(), It.IsAny<ulong>()))
                .Returns(new uint160(0));
            this.callDataSerializer.Setup(x => x.Deserialize(It.IsAny<byte[]>()))
                .Returns(Result.Ok(new ContractTxData(0, 0, (Gas) 0, new uint160(0), null, null)));

            var controller = new SmartContractWalletController(
                this.addressGenerator.Object,
                this.broadcasterManager.Object,
                this.callDataSerializer.Object,
                this.connectionManager.Object,
                this.loggerFactory.Object,
                this.network,
                this.walletManager.Object);

            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as ContractWalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            ContractAccountHistoryModel historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(3, historyModel.TransactionsHistory.Count);
            ContractTransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(2);

            Assert.Equal(ContractTransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(normalTransaction.Id, resultingTransactionModel.Id);
            Assert.Equal(normalTransaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(normalTransaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(1, resultingTransactionModel.ConfirmedInBlock);

            // ElementAt(1) is a Receive

            ContractTransactionItemModel resultingCreateModel = historyModel.TransactionsHistory.ElementAt(0);
            Assert.Equal(ContractTransactionItemType.ContractCreate, resultingCreateModel.Type);
            Assert.Equal(createTransaction.SpendingDetails.TransactionId, resultingCreateModel.Id);
            Assert.Equal(createTransaction.SpendingDetails.Payments.First().Amount, resultingCreateModel.Payments.First().Amount);
            Assert.Equal(uint160.Zero.ToBase58Address(this.network), resultingCreateModel.Payments.First().DestinationAddress);
            Assert.Equal(createTransaction.SpendingDetails.CreationTime, resultingCreateModel.Timestamp);
            Assert.Equal(createTransaction.SpendingDetails.BlockHeight, resultingCreateModel.ConfirmedInBlock);
        }
    }
}
