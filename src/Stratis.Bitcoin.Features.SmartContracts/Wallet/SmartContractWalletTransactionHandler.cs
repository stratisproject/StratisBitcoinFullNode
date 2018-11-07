using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public sealed class SmartContractWalletTransactionHandler : WalletTransactionHandler
    {
        public SmartContractWalletTransactionHandler(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network,
            StandardTransactionPolicy transactionPolicy) :
            base(loggerFactory, walletManager, walletFeePolicy, network, transactionPolicy)
        {
        }

        /// <summary>
        /// The initialization of the builder is overridden as smart contracts calls allow dust and does not group
        /// inputs by ScriptPubKey.
        /// </summary>
        protected override void InitializeTransactionBuilder(TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            context.TransactionBuilder.CoinSelector = new DefaultCoinSelector
            {
                GroupByScriptPubKey = false
            };

            context.TransactionBuilder.DustPrevention = false;
            context.TransactionBuilder.StandardTransactionPolicy = this.TransactionPolicy;

            this.AddRecipients(context);
            this.AddOpReturnOutput(context);
            this.AddCoins(context);
            this.AddSecrets(context);

            if (context.ChangeAddress != null)
                context.TransactionBuilder.SetChange(context.ChangeAddress.ScriptPubKey);
            else
                base.FindChangeAddress(context);

            this.AddFee(context);
        }

        /// <summary>
        /// Adjusted to allow smart contract transactions with zero value through.
        /// </summary>
        protected override void AddRecipients(TransactionBuildContext context)
        {
            if (context.Recipients.Any(recipient => recipient.Amount == Money.Zero && !recipient.ScriptPubKey.IsSmartContractExec()))
                throw new WalletException("No amount specified.");

            if (context.Recipients.Any(a => a.SubtractFeeFromAmount))
                throw new NotImplementedException("Substracting the fee from the recipient is not supported yet.");

            foreach (Recipient recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }
    }
}