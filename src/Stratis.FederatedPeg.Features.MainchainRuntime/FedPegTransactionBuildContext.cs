using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

//This is experimental while we are waiting for a generic OP_RETURN function in the full node wallet.

namespace Stratis.FederatedPeg.Features.MainchainRuntime
{
    public class FedPegTransactionBuildContext : TransactionBuildContext
    {
        public NBitcoin.Script OpReturnScript { get; set; }

        public FedPegTransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients)
            : base(accountReference, recipients, string.Empty)
        {
        }

        public FedPegTransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients,
            string walletPassword, Script opReturnScript)
            : base(accountReference, recipients, walletPassword)
        {
            this.OpReturnScript = opReturnScript;
        }
    }
}
