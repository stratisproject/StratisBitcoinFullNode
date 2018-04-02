using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SidechainWallet
{
    /// <inheritdoc />
    public class TransactionBuildContext : Wallet.TransactionBuildContext
    {
        private TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients)
            : base(accountReference, recipients) { }

        private TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients, string walletPassword)
            : base(accountReference, recipients, walletPassword) { }

        public TransactionBuildContext(Wallet.TransactionBuildContext standardContext, string sidechainIdentifier)
            : base(standardContext.AccountReference, standardContext.Recipients, standardContext.WalletPassword)
        {
            Guard.NotEmpty(sidechainIdentifier, nameof(sidechainIdentifier));
            this.AllowOtherInputs = standardContext.AllowOtherInputs;
            this.ChangeAddress = standardContext.ChangeAddress;
            this.FeeType = standardContext.FeeType;
            this.MinConfirmations = standardContext.MinConfirmations;
            this.OverrideFeeRate = standardContext.OverrideFeeRate;
            this.SelectedInputs = standardContext.SelectedInputs;
            this.Shuffle = standardContext.Shuffle;
            this.SidechainIdentifier = sidechainIdentifier;
            this.Sign = standardContext.Sign;
            this.Transaction = standardContext.Transaction;
            //this.TransactionBuilder = (TransactionBuilder)standardContext.TransactionBuilder;
            this.TransactionFee = standardContext.TransactionFee;
            this.UnspentOutputs = standardContext.UnspentOutputs;
        }

        public string SidechainIdentifier { get; set; }
        public new SidechainWallet.TransactionBuilder TransactionBuilder { get; set; }
    }
}
