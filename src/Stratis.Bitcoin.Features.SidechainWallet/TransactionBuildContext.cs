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
        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients, string sidechainIdentifier) : base(accountReference, recipients)
        {
            Guard.NotEmpty(sidechainIdentifier, nameof(sidechainIdentifier));
            this.SidechainIdentifier = sidechainIdentifier;
        }

        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients, string sidechainIdentifier, string walletPassword) : base(accountReference, recipients, walletPassword)
        {
            Guard.NotEmpty(sidechainIdentifier, nameof(sidechainIdentifier));
            this.SidechainIdentifier = sidechainIdentifier;
        }

        public string SidechainIdentifier { get; set; }
        public new SidechainWallet.TransactionBuilder TransactionBuilder { get; set; }
    }
}
