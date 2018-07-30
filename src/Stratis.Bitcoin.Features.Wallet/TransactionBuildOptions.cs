using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Options object to be passed to <see cref="WalletTransactionHandler"/>.
    /// TODO: Order semantically / alphabetical.
    /// TODO: Make immutable
    /// </summary>
    public class TransactionBuildOptions
    {
        // required
        public WalletAccountReference WalletAccountReference { get; set; }
        
        // required except for estimating
        public string WalletPassword { get; set; }

        // required
        public List<Recipient> Recipients { get; set; }


        public HdAddress ChangeAddress { get; set; }

        public FeeType FeeType { get; set; }

        public int MinConfirmations { get; set; }

        public string OpReturnData { get; set; }

        public FeeRate OverrideFeeRate { get; set; }

        public List<OutPoint> SelectedInputs { get; set; }

        public bool ShuffleOutputs { get; set; }

        public Money TransactionFee { get; set; }

        public TransactionBuildOptions(WalletAccountReference wallet, string password, List<Recipient> recipients)
        {
            Guard.NotNull(recipients, nameof(recipients));

            // Set required fields
            this.WalletAccountReference = wallet;
            this.WalletPassword = password;
            this.Recipients = recipients;

            // Set defaults for options
            this.ChangeAddress = null;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
            this.OpReturnData = null;
            this.OverrideFeeRate = null; // TODO what is this
            this.SelectedInputs = new List<OutPoint>();
            this.ShuffleOutputs = false;
            this.TransactionFee = null;
        }

        /// <summary>
        /// When estimating, don't need password.
        /// </summary>
        public TransactionBuildOptions(WalletAccountReference wallet, List<Recipient> recipients) 
            : this(wallet, null, recipients) {}

    }
}
