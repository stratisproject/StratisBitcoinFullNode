using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.Wallet;
using FluentAssertions;
using Xunit;

namespace Stratis.Bitcoin.Features.SidechainWallet.Tests
{
    public class TransactionBuildContextTest
    {
        [Fact]

        public void Creation_from_Standard_TransactionBuildContext_Should_Fail_Without_SidechainIdentifier()
        {
            var buildContext = new Wallet.TransactionBuildContext(new WalletAccountReference(), new List<Recipient>());
            new Action(() => new TransactionBuildContext(buildContext, null))
                .Should().Throw<ArgumentNullException>();
            new Action(() => new TransactionBuildContext(buildContext, string.Empty))
                .Should().Throw<ArgumentException>();
        }

        [Fact]

        public void Creation_from_Standard_TransactionBuildContext_Should_Not_Fail_With_Valid_SidechainIdentifier()
        {
            var buildContext = new Wallet.TransactionBuildContext(new WalletAccountReference(), new List<Recipient>());
            new Action(() => new TransactionBuildContext(buildContext, "NeitherNullNorEmpty"))
                .Should().NotThrow();
        }

        [Fact]

        public void Creation_from_Standard_TransactionBuildContext_Should_Copy_All_Fields()
        {
            var accountRef = new WalletAccountReference();
            var recipients = new List<Recipient>();
            var password = "P@ssw0rd";
            var changeAddress = new HdAddress();
            var feeRate = new NBitcoin.FeeRate(NBitcoin.Money.CENT);
            var transaction = new NBitcoin.Transaction();
            var inputs = new List<NBitcoin.OutPoint>();
            //var transactionBuilder = new NBitcoin.TransactionBuilder();
            var transactionFee = 2 * NBitcoin.Money.CENT;
            var utxo = new List<UnspentOutputReference>();
            var buildContext = new Wallet.TransactionBuildContext(accountRef, recipients, password)
            {
                AllowOtherInputs = true,
                ChangeAddress = changeAddress,
                FeeType = FeeType.High,
                MinConfirmations = 23,
                OverrideFeeRate = feeRate,
                SelectedInputs = inputs,
                Shuffle = true,
                Sign = true,
                Transaction = transaction,
                //TransactionBuilder = transactionBuilder,
                TransactionFee = transactionFee,
                UnspentOutputs = utxo
            };

            var convertedBuildContext = new TransactionBuildContext(buildContext, "thingy");

            convertedBuildContext.AccountReference.Should().Be(accountRef);
            convertedBuildContext.Recipients.Should().BeSameAs(recipients);
            convertedBuildContext.WalletPassword.Should().Be(password);
            convertedBuildContext.ChangeAddress.Should().Be(changeAddress);
            convertedBuildContext.FeeType.Should().Be(FeeType.High);
            convertedBuildContext.MinConfirmations.Should().Be(23);
            convertedBuildContext.OverrideFeeRate.Should().Be(feeRate);
            convertedBuildContext.SelectedInputs.Should().BeSameAs(inputs);
            convertedBuildContext.Shuffle.Should().BeTrue();
            convertedBuildContext.Sign.Should().BeTrue();
            convertedBuildContext.Transaction.Should().Be(transaction);
            //convertedBuildContext.TransactionBuilder..Should().Be(transaction);
            convertedBuildContext.TransactionFee.Should().BeEquivalentTo(transactionFee);
            convertedBuildContext.UnspentOutputs.Should().BeSameAs(utxo);

        }
    }
}
