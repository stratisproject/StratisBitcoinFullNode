﻿using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Options object to be passed to <see cref="WalletTransactionHandler"/>.
    /// TODO: Order semantically / alphabetical
    /// </summary>
    public class TransactionBuildOptions
    {
        // required
        public WalletAccountReference WalletAccountReference { get; set; }

        // required
        public List<Recipient> Recipients { get; set; }

        // required
        public string WalletPassword { get; set; }

        public string OpReturnData { get; set; }

        public Money TransactionFee { get; set; }

        public FeeType FeeType { get; set; }

        public int MinConfirmations { get; set; }

        public bool ShuffleOutputs { get; set; } 

        public HdAddress ChangeAddress { get; set; }

        public List<OutPoint> SelectedInputs { get; set; }

        public FeeRate OverrideFeeRate { get; set; }

        public TransactionBuildOptions(WalletAccountReference wallet, string password, List<Recipient> recipients)
        {
            this.WalletAccountReference = wallet;
            this.WalletPassword = password;
            this.Recipients = recipients;
        }

        /// <summary>
        /// When estimating, don't need password.
        /// </summary>
        public TransactionBuildOptions(WalletAccountReference wallet, List<Recipient> recipients)
        {
            this.WalletAccountReference = wallet;
            this.Recipients = recipients;
        }

    }
}
