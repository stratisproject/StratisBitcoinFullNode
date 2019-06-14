using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.FederatedPeg.InputConsolidation;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class InputConsolidatorTests : CrossChainTestBase
    {
        [Fact]
        public void ConsolidationTransactionOnlyBuiltWhenWalletHasManyInputs()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);

            var broadcasterManager = new Mock<IBroadcasterManager>();

            var inputConsolidator = new InputConsolidator(
                this.FederationWalletTransactionHandler,
                this.federationWalletManager,
                broadcasterManager.Object,
                this.federatedPegSettings,
                this.loggerFactory,
                this.signals,
                this.network);

            Assert.Null(inputConsolidator.CreateRequiredConsolidationTransactions(Money.Coins(100m)));
        }

        [Fact]
        public void ConsolidationTransactionBuildsCorrectly()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);

            var broadcasterManager = new Mock<IBroadcasterManager>();

            var inputConsolidator = new InputConsolidator(
                this.FederationWalletTransactionHandler,
                this.federationWalletManager,
                broadcasterManager.Object,
                this.federatedPegSettings,
                this.loggerFactory,
                this.signals,
                this.network);

            // Lets set the funding transactions to many really small outputs
            const int numUtxos = FederatedPegSettings.MaxInputs * 2;
            const decimal individualAmount = 0.1m;
            const decimal depositAmount = numUtxos * individualAmount - 1; // Large amount minus some for fees.
            BitcoinAddress address = new Script("").Hash.GetAddress(this.network);

            this.wallet.MultiSigAddress.Transactions.Clear();
            this.fundingTransactions.Clear();

            Money[] funding = new Money[numUtxos];

            for (int i = 0; i < funding.Length; i++)
            {
                funding[i] = new Money(individualAmount, MoneyUnit.BTC);
            }

            this.AddFundingTransaction(funding);

            List<ConsolidationTransaction> transactions = inputConsolidator.CreateRequiredConsolidationTransactions(Money.Coins(depositAmount));

            Assert.Equal(2, transactions.Count);
            Assert.Equal(this.federatedPegSettings.MultiSigAddress.ScriptPubKey, transactions[0].PartialTransaction.Outputs[0].ScriptPubKey);
        }
    }
}
