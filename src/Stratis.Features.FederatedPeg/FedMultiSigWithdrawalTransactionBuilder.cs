using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>
    /// A handler that has various functionalities related to transaction operations.
    /// </summary>
    public class FedMultiSigWithdrawalTransactionBuilder
    {
        private readonly IFederationWalletManager federationWalletManager;

        private readonly IFederatedPegSettings federatedPegSettings;
        
        private readonly Network network;

        private readonly IWalletFeePolicy walletFeePolicy;

        public FedMultiSigWithdrawalTransactionBuilder(
            Network network,
            IFederationWalletManager federationWalletManager,
            IFederatedPegSettings federatedPegSettings,
            IWalletFeePolicy walletFeePolicy)
        {
            this.network = network;
            this.federationWalletManager = federationWalletManager;
            this.federatedPegSettings = federatedPegSettings;
            this.walletFeePolicy = walletFeePolicy;
        }

        public Transaction BuildTransaction(List<Recipient> recipients, Key[] privateKeys)
        {
            // FederationWalletTransactionHandler only supports signing with a single key - the fed wallet key.
            // However we still want to use it to determine what coins we need, so hack this together here to pass in what FederationWalletTransactionHandler.DetermineCoins.
            var multiSigContext = new Wallet.TransactionBuildContext(recipients);

            (List<Coin> coins, List<Wallet.UnspentOutputReference> _) = FederationWalletTransactionHandler.DetermineCoins(this.federationWalletManager, this.network, multiSigContext, this.federatedPegSettings);

            var transactionBuilder = new TransactionBuilder(this.network);

            transactionBuilder.AddCoins(coins);
            transactionBuilder.SetChange(this.federationWalletManager.GetWallet().MultiSigAddress.ScriptPubKey);
            transactionBuilder.AddKeys(privateKeys);
            transactionBuilder.SetTimeStamp(Utils.DateTimeToUnixTime(DateTimeOffset.UtcNow));

            var minTrxFee = new Money(this.network.MinTxFee, MoneyUnit.Satoshi);

            FeeRate feeRate = this.walletFeePolicy.GetFeeRate(FeeType.Medium.ToConfirmations());

            long fee = Math.Max(transactionBuilder.EstimateFees(feeRate), minTrxFee);

            transactionBuilder.SendFees(fee);

            foreach (Recipient recipient in recipients)
            {
                transactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
            }

            Transaction transaction = transactionBuilder.BuildTransaction(true);

            return transaction;
        }
    }
}