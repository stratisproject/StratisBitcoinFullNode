using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public sealed class FederationWalletManagerTests
    {
        private readonly Network network;
        private readonly BitcoinAddress federationMultiSigAddress;

        public FederationWalletManagerTests()
        {
            this.network = new StratisMain();

            var base58 = new Key().PubKey.GetAddress(this.network).ToString();
            this.federationMultiSigAddress = BitcoinAddress.Create(base58, this.network);
        }

        /// <summary>
        /// In a federation wallet multisig scenario, transactions will be constantly added to 
        /// the wallet until all the federation members has signed the transaction. In this case,
        /// transactions and their spending details are constantly updated until the fully signed transaction
        /// is confirmed in a block.
        /// 
        /// It is entirely possible that during this process a partially signed transaction added to the wallet
        /// that spends another transaction is quickly superceded by a more signed partial transaction, also spending said transaction. 
        /// In this case, the previously, less signed transaction should be removed from the wallet as it is no longer applicable.
        /// </summary>
        [Fact]
        public void RemoveTransactions_When_OverwritingSpendDetails()
        {
            FederationWalletManager federationWalletManager = CreateFederationWalletManager();

            // Create the initial transaction and add it to the wallet.
            Transaction transactionA = this.network.CreateTransaction();
            transactionA.AddOutput(new TxOut(Money.Coins(10), this.federationMultiSigAddress));
            federationWalletManager.ProcessTransaction(transactionA);

            // Verify that transaction A is present and unspent.
            federationWalletManager.Wallet.MultiSigAddress.Transactions.TryGetTransaction(transactionA.GetHash(), 0, out TransactionData addedTx_A);
            Assert.NotNull(addedTx_A);
            Assert.Null(addedTx_A.SpendingDetails);

            // Create a spending transaction that spends transaction A
            Transaction transactionB = this.network.CreateTransaction();
            transactionB.Time = transactionA.Time + 1;
            transactionB.AddInput(transactionA, 0);
            transactionB.AddOutput(new TxOut(Money.Coins(5), this.federationMultiSigAddress));
            federationWalletManager.ProcessTransaction(transactionB);

            // Verify that transaction B is present and unspent.
            federationWalletManager.Wallet.MultiSigAddress.Transactions.TryGetTransaction(transactionB.GetHash(), 0, out TransactionData addedTx_B);
            Assert.NotNull(addedTx_B);
            Assert.Null(addedTx_B.SpendingDetails);

            // Verify that transaction B now spends transaction A.
            federationWalletManager.Wallet.MultiSigAddress.Transactions.TryGetTransaction(transactionA.GetHash(), 0, out addedTx_A);
            Assert.NotNull(addedTx_A);
            Assert.NotNull(addedTx_A.SpendingDetails);
            Assert.Equal(addedTx_A.SpendingDetails.TransactionId, transactionB.GetHash());

            // Create another spending transaction that also spends transaction A
            Transaction transactionC = this.network.CreateTransaction();
            transactionC.Time = transactionB.Time + 1;
            transactionC.AddInput(transactionA, 0);
            transactionC.AddOutput(new TxOut(Money.Coins(5), this.federationMultiSigAddress));
            federationWalletManager.ProcessTransaction(transactionC);

            // Verify that transaction C is present and unspent.
            federationWalletManager.Wallet.MultiSigAddress.Transactions.TryGetTransaction(transactionC.GetHash(), 0, out TransactionData addedTx_C);
            Assert.NotNull(addedTx_C);
            Assert.Null(addedTx_C.SpendingDetails);

            // Verify that transaction C now spends transaction A.
            federationWalletManager.Wallet.MultiSigAddress.Transactions.TryGetTransaction(transactionA.GetHash(), 0, out addedTx_A);
            Assert.NotNull(addedTx_A);
            Assert.NotNull(addedTx_A.SpendingDetails);
            Assert.Equal(addedTx_A.SpendingDetails.TransactionId, transactionC.GetHash());

            // Verify that transaction B was now removed.
            federationWalletManager.Wallet.MultiSigAddress.Transactions.TryGetTransaction(transactionB.GetHash(), 0, out addedTx_B);
            Assert.Null(addedTx_B);
        }

        private FederationWalletManager CreateFederationWalletManager()
        {
            var logger = new Mock<ILogger>();
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            var nodeLifetime = new NodeLifetime();
            var signals = new Mock<ISignals>();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals.Object, nodeLifetime);
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            var chainIndexer = new ChainIndexer(this.network);

            var federatedPegSettings = new Mock<IFederatedPegSettings>();
            federatedPegSettings.Setup(f => f.MultiSigAddress).Returns(this.federationMultiSigAddress);

            // Create the wallet manager.
            var federationWalletManager = new FederationWalletManager(
                loggerFactory.Object,
                this.network,
                chainIndexer,
                dataFolder,
                new Mock<IWalletFeePolicy>().Object,
                asyncProvider,
                nodeLifetime,
                new Mock<IDateTimeProvider>().Object,
                federatedPegSettings.Object,
                new Mock<IWithdrawalExtractor>().Object,
                new Mock<IBlockRepository>().Object);

            federationWalletManager.Start();

            return federationWalletManager;
        }
    }
}
