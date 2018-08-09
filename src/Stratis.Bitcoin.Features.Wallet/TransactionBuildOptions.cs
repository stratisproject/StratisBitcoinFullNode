using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Options object to be passed to <see cref="WalletTransactionHandler"/>.
    /// </summary>
    public class TransactionBuildOptions
    {
        /// <summary>
        /// Wallet account to use to build transaction. This is the account from which UTXOs will be selected. Required.
        /// </summary>
        public WalletAccountReference WalletAccountReference { get; set; }

        /// <summary>
        /// Password for the given <see cref="WalletAccountReference"/>. Required to sign and send transactions.
        /// </summary>
        /// <remarks>
        /// TODO: replace this with System.Security.SecureString (https://github.com/dotnet/corefx/tree/master/src/System.Security.SecureString)
        /// More info (https://github.com/dotnet/corefx/issues/1387)
        /// </remarks>
        public string WalletPassword { get; set; }

        /// <summary>
        /// Addresses and amounts to send funds to. Required.
        /// </summary>
        public List<Recipient> Recipients { get; set; }

        /// <summary>
        /// Address to send change back to. Must belong to <see cref="WalletAccountReference"/>.
        /// </summary>
        public HdAddress ChangeAddress { get; set; }

        /// <summary>
        /// Whether to send a low, medium, or high fee. Defaults to medium.
        /// </summary>
        public FeeType FeeType { get; set; }

        /// <summary>
        /// Minimum number of confirmations for UTXOs to use as inputs for this transaction.
        /// </summary>
        public int MinConfirmations { get; set; }

        /// <summary>
        /// Data to be saved on-chain using an OP_RETURN.
        /// </summary>
        public string OpReturnData { get; set; }

        /// <summary>
        /// Overrides the fee rate from the <see cref="WalletFeePolicy"/> that would otherwise be used by the <see cref="WalletTransactionHandler"/>
        /// </summary>
        public FeeRate OverrideFeeRate { get; set; }

        /// <summary>
        /// Explicitly set the inputs that should be used for this transaction, rather than having the <see cref="WalletTransactionHandler"/> pick them itself.
        /// </summary>
        public List<OutPoint> SelectedInputs { get; set; }

        /// <summary>
        /// Whether to randomly order the outputs of the transaction to preserve privacy.
        /// </summary>
        public bool ShuffleOutputs { get; set; }

        /// <summary>
        /// Set the transaction fee explicitly for this transaction.
        /// </summary>
        public Money TransactionFee { get; set; }

        /// <summary>
        /// Initializes a new options object with open wallet and addresses to send to. 
        /// </summary>
        public TransactionBuildOptions(WalletAccountReference walletAccountReference, string password, List<Recipient> recipients)
        {
            Guard.NotNull(recipients, nameof(recipients));

            this.WalletAccountReference = walletAccountReference;
            this.WalletPassword = password;
            this.Recipients = recipients;
            InitializeDefaults();
        }

        /// <summary>
        /// When estimating, don't need password.
        /// </summary>
        public TransactionBuildOptions(WalletAccountReference walletAccountReference, List<Recipient> recipients) 
            : this(walletAccountReference, null, recipients) {}

        /// <summary>
        /// Set default values on all optional properties.
        /// </summary>
        private void InitializeDefaults()
        {
            this.ChangeAddress = null;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
            this.OpReturnData = null;
            this.OverrideFeeRate = null;
            this.SelectedInputs = new List<OutPoint>();
            this.ShuffleOutputs = false;
            this.TransactionFee = null;
        }

    }
}
