using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A handler that has various functionalities related to transaction operations.
    /// </summary>
    public class MultisigTransactionHandler : WalletTransactionHandler, IMultisigTransactionHandler
    {
        private readonly ILogger logger;

        private readonly Network network;
        
        private readonly StandardTransactionPolicy transactionPolicy;

        private readonly IWalletManager walletManager;

        private readonly IWalletFeePolicy walletFeePolicy;

        public MultisigTransactionHandler(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network,
            StandardTransactionPolicy transactionPolicy)
            : base(loggerFactory, walletManager, walletFeePolicy, network, transactionPolicy)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context, string[] mnemonics)
        {
            if (mnemonics == null || mnemonics.Length == 0)
               throw new WalletException($"Could not build the transaction. Details: no private keys provided");

            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
                context.TransactionBuilder.Shuffle();

            Transaction unsignedTransaction = context.TransactionBuilder.BuildTransaction(false);
            var signedTransactions = new List<Transaction>();
            foreach (string secret in mnemonics)
            {
                var mnemonic = new Mnemonic(secret);
                ExtKey extKey = mnemonic.DeriveExtKey();
                Transaction transaction = context.TransactionBuilder.AddKeys(extKey.PrivateKey).SignTransaction(unsignedTransaction);
                signedTransactions.Add(transaction);
            }

            Transaction combinedTransaction = context.TransactionBuilder.CombineSignatures(signedTransactions.ToArray());

            if (context.TransactionBuilder.Verify(combinedTransaction, out TransactionPolicyError[] errors))
                return combinedTransaction;

            string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
            this.logger.LogError($"Build transaction failed: {errorsMessage}");
            throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
        }

        /// <summary>
        /// Initializes the context transaction builder from information in <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">Transaction build context.</param>
        protected override void InitializeTransactionBuilder(TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            // If inputs are selected by the user, we just choose them all.
            if (context.SelectedInputs != null && context.SelectedInputs.Any())
            {
                context.TransactionBuilder.CoinSelector = new AllCoinsSelector();
            }

            this.AddRecipients(context);
            this.AddOpReturnOutput(context);
            this.AddCoins(context);
            this.FindChangeAddress(context);
            this.AddFee(context);

            if (context.Time.HasValue)
                context.TransactionBuilder.SetTimeStamp(context.Time.Value);
        }
    }
}
