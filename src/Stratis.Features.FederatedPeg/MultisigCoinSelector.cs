using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg
{
    public interface IMultisigCoinSelector
    {
        (List<Coin>, List<UnspentOutputReference>) SelectCoins(List<Recipient> recipients);
    }

    /// <summary>
    /// Used to wrap a static method with a class that can be mocked for testing.
    /// </summary>
    public class MultisigCoinSelector : IMultisigCoinSelector
    {
        private readonly Network network;

        private readonly IFederatedPegSettings settings;
        private readonly IFederationWalletManager walletManager;

        public MultisigCoinSelector(Network network, IFederatedPegSettings settings, IFederationWalletManager walletManager)
        {
            this.network = network;
            this.settings = settings;
            this.walletManager = walletManager;
        }

        public (List<Coin>, List<UnspentOutputReference>) SelectCoins(List<Recipient> recipients)
        {
            // FederationWalletTransactionHandler only supports signing with a single key - the fed wallet key - so we don't use it to build the transaction.
            // However we still want to use it to determine what coins we need, so hack this together here to pass in to FederationWalletTransactionHandler.DetermineCoins.
            var multiSigContext = new TransactionBuildContext(recipients);

            return FederationWalletTransactionHandler.DetermineCoins(this.walletManager, this.network, multiSigContext, this.settings);
        }
    }
}