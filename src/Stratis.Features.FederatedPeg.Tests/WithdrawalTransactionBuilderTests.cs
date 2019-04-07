using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;
using Stratis.Sidechains.Networks;
using Xunit;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;
using TransactionBuildContext = Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class WithdrawalTransactionBuilderTests
    {
        private readonly Network network;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<ILogger> logger;
        private readonly Mock<IFederationWalletManager> federationWalletManager;
        private readonly Mock<IFederationWalletTransactionHandler> federationWalletTransactionHandler;
        private readonly Mock<IFederationGatewaySettings> federationGatewaySettings;

        public WithdrawalTransactionBuilderTests()
        {
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();
            this.federationWalletManager = new Mock<IFederationWalletManager>();
            this.federationWalletTransactionHandler = new Mock<IFederationWalletTransactionHandler>();
            this.federationGatewaySettings = new Mock<IFederationGatewaySettings>();

            this.logger = new Mock<ILogger>();
            this.loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(this.logger.Object);

            this.federationGatewaySettings.Setup(x => x.TransactionFee)
                .Returns(FederationGatewaySettings.DefaultTransactionFee);

            this.federationWalletManager.Setup(x => x.Secret)
                .Returns(new WalletSecret());

            this.federationWalletTransactionHandler.Setup(x => x.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(this.network.CreateTransaction());
        }

        [Fact]
        public void FeeIsTakenFromRecipient()
        {
            var txBuilder = new WithdrawalTransactionBuilder(
                this.loggerFactory.Object,
                this.network,
                this.federationWalletManager.Object,
                this.federationWalletTransactionHandler.Object,
                this.federationGatewaySettings.Object
                );

            var recipient = new Recipient
            {
                Amount = Money.Coins(101),
                ScriptPubKey = new Script()
            };

            Transaction ret = txBuilder.BuildWithdrawalTransaction(uint256.One, 100, recipient);

            Assert.NotNull(ret);

            Money expectedAmountAfterFee = recipient.Amount - this.federationGatewaySettings.Object.TransactionFee;

            this.federationWalletTransactionHandler.Verify(x => x.BuildTransaction(It.Is<TransactionBuildContext>(y => y.Recipients.First().Amount == expectedAmountAfterFee)));
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
                this.federationGatewaySettings.Object
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
    }
}
