using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Controllers
{
    public class SmartContractWalletControllerTest
    {
        private readonly Mock<IBroadcasterManager> broadcasterManager;
        private readonly Mock<ICallDataSerializer> callDataSerializer;
        private readonly Mock<IConnectionManager> connectionManager;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Network network;
        private readonly Mock<IReceiptRepository> receiptRepository;
        private readonly Mock<IWalletManager> walletManager;
        private Mock<ISmartContractTransactionService> smartContractTransactionService;

        public SmartContractWalletControllerTest()
        {
            this.broadcasterManager = new Mock<IBroadcasterManager>();
            this.callDataSerializer = new Mock<ICallDataSerializer>();
            this.connectionManager = new Mock<IConnectionManager>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.network = new SmartContractsRegTest();
            this.receiptRepository = new Mock<IReceiptRepository>();
            this.walletManager = new Mock<IWalletManager>();
            this.smartContractTransactionService = new Mock<ISmartContractTransactionService>();
        }

        [Fact]
        public void GetHistoryWithValidModelWithoutTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice;
            int vmVersion = 1;
            var gasLimit = (Stratis.SmartContracts.RuntimeObserver.Gas)(SmartContractFormatLogic.GasLimitMaximum / 2);
            var contractTxData = new ContractTxData(vmVersion, gasPrice, gasLimit, new byte[] { 0, 1, 2, 3 });
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
            this.walletManager.Setup(w => w.GetHistory(walletName, It.IsAny<string>())).Returns(accountsHistory);
            this.walletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);
            this.walletManager.Setup(w => w.GetAccounts(walletName)).Returns(new List<HdAccount> { account });

            var receipt = new Receipt(null, 12345, new Log[0], null, null, null, uint160.Zero, true, null, null, 2, 100000);
            this.receiptRepository.Setup(x => x.Retrieve(It.IsAny<uint256>()))
                .Returns(receipt);
            this.callDataSerializer.Setup(x => x.Deserialize(It.IsAny<byte[]>()))
                .Returns(Result.Ok(new ContractTxData(0, 0, (Stratis.SmartContracts.RuntimeObserver.Gas)0, new uint160(0), null, null)));

            var controller = new SmartContractWalletController(
                this.broadcasterManager.Object,
                this.callDataSerializer.Object,
                this.connectionManager.Object,
                this.loggerFactory.Object,
                this.network,
                this.receiptRepository.Object,
                this.walletManager.Object,
                this.smartContractTransactionService.Object);

            IActionResult result = controller.GetHistory(walletName, address.Address);

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<ContractTransactionItem>;

            Assert.NotNull(model);
            Assert.Single(model);

            ContractTransactionItem resultingTransaction = model.ElementAt(0);

            ContractTransactionItem resultingCreate = model.ElementAt(0);
            Assert.Equal(ContractTransactionItemType.ContractCreate, resultingTransaction.Type);
            Assert.Equal(createTransaction.SpendingDetails.TransactionId, resultingTransaction.Hash);
            Assert.Equal(createTransaction.SpendingDetails.Payments.First().Amount.ToUnit(MoneyUnit.Satoshi), resultingTransaction.Amount);
            Assert.Equal(uint160.Zero.ToBase58Address(this.network), resultingTransaction.To);
            Assert.Equal(createTransaction.SpendingDetails.BlockHeight, resultingTransaction.BlockHeight);
            Assert.Equal((createTransaction.Amount - createTransaction.SpendingDetails.Payments.First().Amount).ToUnit(MoneyUnit.Satoshi), resultingTransaction.TransactionFee);
            Assert.Equal(receipt.GasPrice * receipt.GasUsed, resultingTransaction.GasFee);
        }
    }
}
