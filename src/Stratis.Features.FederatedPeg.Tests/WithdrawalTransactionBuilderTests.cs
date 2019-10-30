using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;
using Stratis.Sidechains.Networks;
using Xunit;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;
using TransactionBuildContext = Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext;
using UnspentOutputReference = Stratis.Features.FederatedPeg.Wallet.UnspentOutputReference;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class WithdrawalTransactionBuilderTests
    {
        private readonly Network network;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<ILogger> logger;
        private readonly Mock<IFederationWalletManager> federationWalletManager;
        private readonly Mock<IFederationWalletTransactionHandler> federationWalletTransactionHandler;
        private readonly Mock<IFederatedPegSettings> federationGatewaySettings;
        private readonly Mock<ISignals> signals;

        public WithdrawalTransactionBuilderTests()
        {
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.network = CirrusNetwork.NetworksSelector.Regtest();
            this.federationWalletManager = new Mock<IFederationWalletManager>();
            this.federationWalletTransactionHandler = new Mock<IFederationWalletTransactionHandler>();
            this.federationGatewaySettings = new Mock<IFederatedPegSettings>();
            this.signals = new Mock<ISignals>();

            this.logger = new Mock<ILogger>();
            this.loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(this.logger.Object);

            this.federationGatewaySettings.Setup<Money>(x => x.GetWithdrawalTransactionFee(It.IsAny<int>()))
                .Returns<int>((numInputs) => {
                    return FederatedPegSettings.BaseTransactionFee + FederatedPegSettings.InputTransactionFee * numInputs;
                });

            this.federationWalletManager.Setup(x => x.Secret)
                .Returns(new WalletSecret());

            this.federationWalletTransactionHandler.Setup(x => x.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(this.network.CreateTransaction());
        }

        [Fact]
        public void FeeIsTakenFromRecipient()
        {
            Script redeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, new[] {new Key().PubKey, new Key().PubKey});

            this.federationWalletManager.Setup(x => x.GetSpendableTransactionsInWallet(It.IsAny<int>()))
                .Returns(new List<UnspentOutputReference>
                {
                    new UnspentOutputReference
                    {
                        Transaction = new FederatedPeg.Wallet.TransactionData
                        {
                            Amount = Money.Coins(105),
                            Id = uint256.One,
                            ScriptPubKey = redeemScript.Hash.ScriptPubKey
                        }
                    }
                });

            this.federationWalletManager.Setup(x => x.GetWallet())
                .Returns(new FederationWallet
                {
                    MultiSigAddress = new MultiSigAddress
                    {
                        RedeemScript = redeemScript
                    }
                });

            var txBuilder = new WithdrawalTransactionBuilder(
                this.loggerFactory.Object,
                this.network,
                this.federationWalletManager.Object,
                this.federationWalletTransactionHandler.Object,
                this.federationGatewaySettings.Object,
                this.signals.Object
                );

            var recipient = new Recipient
            {
                Amount = Money.Coins(101),
                ScriptPubKey = new Script()
            };

            Transaction ret = txBuilder.BuildWithdrawalTransaction(uint256.One, 100, recipient);

            Assert.NotNull(ret);

            // Fee taken from amount should be the total fee.
            Money expectedAmountAfterFee = recipient.Amount - FederatedPegSettings.CrossChainTransferFee;
            this.federationWalletTransactionHandler.Verify(x => x.BuildTransaction(It.Is<TransactionBuildContext>(y => y.Recipients.First().Amount == expectedAmountAfterFee)));

            // Fee used to send transaction should be a smaller amount.
            Money expectedTxFee = FederatedPegSettings.BaseTransactionFee + 1 * FederatedPegSettings.InputTransactionFee;
            this.federationWalletTransactionHandler.Verify(x => x.BuildTransaction(It.Is<TransactionBuildContext>(y => y.TransactionFee == expectedTxFee)));
        }

        [Fact]
        public void NoSpendableTransactionsLogWarning()
        {
            // Throw a 'no spendable transactions' exception
            this.federationWalletTransactionHandler.Setup(x => x.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Throws(new WalletException(FederationWalletTransactionHandler.NoSpendableTransactionsMessage));

            var txBuilder = new WithdrawalTransactionBuilder(
                this.loggerFactory.Object,
                this.network,
                this.federationWalletManager.Object,
                this.federationWalletTransactionHandler.Object,
                this.federationGatewaySettings.Object,
                this.signals.Object
            );

            var recipient = new Recipient
            {
                Amount = Money.Coins(101),
                ScriptPubKey = new Script()
            };

            Transaction ret = txBuilder.BuildWithdrawalTransaction(uint256.One, 100, recipient);

            // Log out a warning in this case, not an error.
            this.logger.Verify(x=>x.Log<object>(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()));
        }

        [Fact]
        public void NotEnoughFundsLogWarning()
        {
            // Throw a 'no spendable transactions' exception
            this.federationWalletTransactionHandler.Setup(x => x.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Throws(new WalletException(FederationWalletTransactionHandler.NotEnoughFundsMessage));

            var txBuilder = new WithdrawalTransactionBuilder(
                this.loggerFactory.Object,
                this.network,
                this.federationWalletManager.Object,
                this.federationWalletTransactionHandler.Object,
                this.federationGatewaySettings.Object,
                this.signals.Object
            );

            var recipient = new Recipient
            {
                Amount = Money.Coins(101),
                ScriptPubKey = new Script()
            };

            Transaction ret = txBuilder.BuildWithdrawalTransaction(uint256.One, 100, recipient);

            // Log out a warning in this case, not an error.
            this.logger.Verify(x => x.Log<object>(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()));
        }
    }
}
