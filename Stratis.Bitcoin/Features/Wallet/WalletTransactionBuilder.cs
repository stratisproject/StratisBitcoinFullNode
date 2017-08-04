using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Features.Wallet
{
    public interface IWalletTransactionBuilder
    {
        /// <summary>
        /// Build a new transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(TransactionBuildContext context);
    }

    /// <summary>
    /// A builder that uses various parameters to build a Bitcoin transaction.
    /// </summary>
    /// <remarks>
    /// This will uses the FeeEstimator and the TrasnactionBuilder.
    /// TODO: Move also the broadcast transaction to this class
    /// </remarks>
    public class WalletTransactionBuilder : IWalletTransactionBuilder
    {
        private readonly ConcurrentChain chain;
        private readonly IWalletManager walletManager;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly Network network;
        private readonly CoinType coinType;
        private readonly ILogger logger;

        public WalletTransactionBuilder(ILoggerFactory loggerFactory, ConcurrentChain chain, IWalletManager walletManager, IWalletFeePolicy walletFeePolicy, Network network)
        {
            this.chain = chain;
            this.walletManager = walletManager;
            this.walletFeePolicy = walletFeePolicy;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            context.TransactionBuilder = new TransactionBuilder();

            this.AddRecepients(context);
            this.AddCoins(context);
            this.AddSecrets(context);
            this.FindChangeAddress(context);
            this.AddFee(context);

            // build transaction
            context.Transaction = context.TransactionBuilder.BuildTransaction(true);

            if (!context.TransactionBuilder.Verify(context.Transaction, out TransactionPolicyError[] errors))
            {
                this.logger.LogError($"Build transaction failed: {string.Join(" - ", errors.Select(s => s.ToString()))}");

                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return context.Transaction;
        }

        /// <summary>
        /// Load all the private keys for each of the <see cref="HdAddress"/> in <see cref="TransactionBuildContext.UnspentOutputs"/>
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddSecrets(TransactionBuildContext context)
        {
            Wallet wallet = this.walletManager.GetWalletByName(context.AccountReference.WalletName);

            // get extended private key
            var privateKey = Key.Parse(wallet.EncryptedSeed, context.WalletPassword.ToString(), wallet.Network);
            var seedExtKey = new ExtKey(privateKey, wallet.ChainCode);

            var signingKeys = new HashSet<ISecret>();
            var added = new HashSet<HdAddress>();
            foreach (var unspentOutputsItem in context.UnspentOutputs.Items)
            {
                if(added.Contains(unspentOutputsItem.Address))
                    continue;
                
                var address = unspentOutputsItem.Address;
                ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(address.HdPath));
                BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(wallet.Network);
                signingKeys.Add(addressPrivateKey);
                added.Add(unspentOutputsItem.Address);
            }

            context.TransactionBuilder.AddKeys(signingKeys.ToArray());
        }

        /// <summary>
        /// Find the next available change address.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void FindChangeAddress(TransactionBuildContext context)
        {
            Wallet wallet = this.walletManager.GetWalletByName(context.AccountReference.WalletName);
            HdAccount account = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType)
                .GetAccountByName(context.AccountReference.AccountName);

            // get address to send the change to
            context.ChangeAddress = this.walletManager.GetOrCreateChangeAddress(account);
            context.TransactionBuilder.SetChange(context.ChangeAddress.ScriptPubKey);

        }

        /// <summary>
        /// Find all available outputs (UTXO's) that belong to <see cref="WalletAccountReference.AccountName"/>. 
        /// Then add them to the <see cref="TransactionBuildContext.UnspentOutputs"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddCoins(TransactionBuildContext context)
        {
            context.UnspentOutputs = this.walletManager.GetSpendableTransactions(context.AccountReference, context.MinConfirmations);

            if (context.UnspentOutputs.Items.Count == 0)
            {
                throw new WalletException($"No spendable transactions found on account {context.AccountReference.AccountName}.");
            }

            // get total spendable balance in the account.
            var balance = context.UnspentOutputs.Items.Sum(t => t.Transaction.Amount);
            if (balance < context.Recipients.Sum(s => s.Amount))
                throw new WalletException("Not enough funds.");

            var coins = new List<Coin>();
            foreach (var item in context.UnspentOutputs.Items)
            {
                coins.Add(new Coin(item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey));
            }

            // All the UTXO is added to the builder without filtering.
            // The builder then has its own coin selection mechanism 
            // to select the best UTXO set for the corresponding amount.
            // To add a custom implementation of a coin selection override 
            // the builder using builder.SetCoinSelection()

            context.TransactionBuilder.AddCoins(coins);
        }

        /// <summary>
        /// Add recipients to the <see cref="TransactionBuilder"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <remarks>
        /// Add outputs to the <see cref="TransactionBuilder"/> based on the <see cref="Recipient"/> list.
        /// </remarks>
        private void AddRecepients(TransactionBuildContext context)
        {
            if (context.Recipients.Any(a => a.Amount == Money.Zero))
                throw new WalletException($"No amount specified");

            foreach (var recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        /// <summary>
        /// Use the <see cref="FeeRate"/> from the <see cref="walletFeePolicy"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddFee(TransactionBuildContext context)
        {
            var feeRate = this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
            var fee = context.TransactionBuilder.EstimateFees(feeRate);
            context.TransactionBuilder.SendFees(fee);
            context.TransactionFee = fee;
        }
    }

    public class TransactionBuildContext
    {
        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="accountReference">The wallet and account from which to build this transaction</param>
        /// <param name="recipients">The target recipients to send coins to.</param>
        /// <param name="walletPassword">The password that protects the wallet in <see cref="accountReference"/></param>
        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients, string walletPassword)
        {
            this.AccountReference = accountReference;
            this.Recipients = recipients;
            this.WalletPassword = walletPassword;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
        }

        /// <summary>
        /// The wallet account to use for building a transaction
        /// </summary>
        public WalletAccountReference AccountReference { get; set; }

        /// <summary>
        /// The recipients to send Bitcoin to.
        /// </summary>
        public List<Recipient> Recipients { get; set; }

        /// <summary>
        /// Helper to estimate how much fee to spend on a transaction.
        /// </summary>
        /// <remarks>
        /// The higher the fee the faster a transaction will get in to a block. 
        /// </remarks>
        public FeeType FeeType { get; set; }

        public int MinConfirmations { get; set; }

        /// <summary>
        /// Coins that are available to be spent.
        /// </summary>
        /// <remarks>Only outputs from a single account are represented in <see cref="UnspentAccountReference"/>.</remarks>
        public UnspentAccountReference UnspentOutputs { get; set; }

        /// <summary>
        /// The builder used to build the current transaction.
        /// </summary>
        public TransactionBuilder TransactionBuilder { get; set; }

        /// <summary>
        /// The change address, where any remaining funds will be sent to.
        /// </summary>
        /// <remarks>
        /// A Bitcoin has to spend the entire UTXO, if total value is greater then the send target 
        /// the rest of the coins go in to a change address that is under the senders control.
        /// </remarks>
        public HdAddress ChangeAddress { get; set; }

        /// <summary>
        /// The total fee on the transaction.
        /// </summary>
        public Money TransactionFee { get; set; }

        /// <summary>
        /// The final transaction.
        /// </summary>
        public Transaction Transaction { get; set; }

        /// <summary>
        /// The password that protects the wallet in <see cref="WalletAccountReference"/>.
        /// </summary>
        /// <remarks>
        /// TODO: replace this with System.Security.SecureString (https://github.com/dotnet/corefx/tree/master/src/System.Security.SecureString)
        /// More info (https://github.com/dotnet/corefx/issues/1387)
        /// </remarks>
        public string WalletPassword { get; set; }
    }

    /// <summary>
    /// Represents recipients of a payment, used in <see cref="WalletTransactionBuilder.BuildTransaction"/> 
    /// </summary>
    public class Recipient
    {
        /// <summary>
        /// The destination script.
        /// </summary>
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The amount that will be sent
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// An indicator if the fee is subtracted from the current recipient.
        /// </summary>
        public bool SubtractFeeFromAmount { get; set; }
    }
}