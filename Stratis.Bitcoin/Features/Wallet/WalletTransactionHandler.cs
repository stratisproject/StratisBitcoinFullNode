using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Features.Wallet
{
    public interface IWalletTransactionHandler
    {
        /// <summary>
        /// Builds a new transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(TransactionBuildContext context);

        /// <summary>
        /// Adds inputs to a transaction until it has enough in value to meet its out value.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <param name="transaction">The transaction that will have more inputs added to it.</param>
        /// <remarks>
        /// This will not modify existing inputs, and will add at most one change output to the outputs.
        /// No existing outputs will be modified unless <see cref="Recipient.SubtractFeeFromAmount"/> is specified.
        /// Note that inputs which were signed may need to be resigned after completion since in/outputs have been added.
        /// The inputs added may be signed depending on <see cref="TransactionBuildContext.Sign"/>, use signrawtransaction for that.
        /// Note that all existing inputs must have their previous output transaction be in the wallet.
        /// </remarks>
        void FundTransaction(TransactionBuildContext context, Transaction transaction);

        /// <summary>
        /// Calculates the maximum amount a user can spend in a single transaction, taking into account the fees required.
        /// </summary>
        /// <param name="accountReference">The account from which to calculate the amount.</param>
        /// <param name="feeType">The type of fee used to calculate the maximum amount the user can spend. The higher the fee, the smaller this amount will be.</param>
        /// <param name="allowUnconfirmed"><c>true</c> to include unconfirmed transactions in the calculation, <c>false</c> otherwise.</param>
        /// <returns>The maximum amount the user can spend in a single transaction, along with the fee required.</returns>
        (Money maximumSpendableAmount, Money Fee) GetMaximumSpendableAmount(WalletAccountReference accountReference, FeeType feeType, bool allowUnconfirmed);
    }

    /// <summary>
    /// A handler that has various functionalities related to transaction operations.
    /// </summary>
    /// <remarks>
    /// This will uses the <see cref="IWalletFeePolicy"/> and the <see cref="TransactionBuilder"/>.
    /// TODO: Move also the broadcast transaction to this class
    /// TODO: Implement lockUnspents
    /// TODO: Implement subtractFeeFromOutputs
    /// </remarks>
    public class WalletTransactionHandler : IWalletTransactionHandler
    {
        private readonly ConcurrentChain chain;
        private readonly IWalletManager walletManager;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly Network network;
        private readonly CoinType coinType;
        private readonly ILogger logger;

        public WalletTransactionHandler(
            ILoggerFactory loggerFactory, 
            ConcurrentChain chain, 
            IWalletManager walletManager, 
            IWalletFeePolicy walletFeePolicy, 
            Network network)
        {
            this.chain = chain;
            this.walletManager = walletManager;
            this.walletFeePolicy = walletFeePolicy;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;            
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            context.TransactionBuilder = new TransactionBuilder();

            this.AddRecipients(context);
            this.AddCoins(context);
            this.AddSecrets(context);
            this.FindChangeAddress(context);
            this.AddFee(context);

            // build transaction
            context.Transaction = context.TransactionBuilder.BuildTransaction(context.Sign);

            if (!context.TransactionBuilder.Verify(context.Transaction, out TransactionPolicyError[] errors))
            {
                this.logger.LogError($"Build transaction failed: {string.Join(" - ", errors.Select(s => s.ToString()))}");

                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return context.Transaction;
        }

        /// <inheritdoc />
        public void FundTransaction(TransactionBuildContext context, Transaction transaction)
        {
            if (context.Recipients.Any())
                throw new WalletException("Adding outputs is not allowed");

            // Turn the txout set into a Recipient array
            context.Recipients.AddRange(transaction.Outputs
                .Select(s => new Recipient
                {
                    ScriptPubKey = s.ScriptPubKey,
                    Amount = s.Value,
                    SubtractFeeFromAmount = false // default for now
                }));

            context.AllowOtherInputs = true;

            foreach (var transactionInput in transaction.Inputs)
                context.SelectedInputs.Add(transactionInput.PrevOut);

            var newTransaction = this.BuildTransaction(context);

            if (context.ChangeAddress != null)
            {
                // find the position of the change and move it over.
                var index = 0;
                foreach (var newTransactionOutput in newTransaction.Outputs)
                {
                    if (newTransactionOutput.ScriptPubKey == context.ChangeAddress.ScriptPubKey)
                    {
                        transaction.Outputs.Insert(index, newTransactionOutput);
                    }

                    index++;
                }
            }

            // TODO: copy the new output amount size (this also includes spreading the fee over all outputs)

            // copy all the inputs from the new transaction.
            foreach (var newTransactionInput in newTransaction.Inputs)
            {
                if (!context.SelectedInputs.Contains(newTransactionInput.PrevOut))
                {
                    transaction.Inputs.Add(newTransactionInput);

                    // TODO: build a mechanism to lock inputs
                }
            }
        }

        /// <inheritdoc />
        public (Money maximumSpendableAmount, Money Fee) GetMaximumSpendableAmount(WalletAccountReference accountReference, FeeType feeType, bool allowUnconfirmed)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.NotEmpty(accountReference.WalletName, nameof(accountReference.WalletName));
            Guard.NotEmpty(accountReference.AccountName, nameof(accountReference.AccountName));
            
            // Get the total value of spendable coins in the account.
            var maxSpendableAmount = this.walletManager.GetSpendableTransactions(accountReference, allowUnconfirmed ? 0 : 1).UnspentOutputs.Sum(x => x.Transaction.Amount);

            // Return 0 if the user has nothing to spend.
            if (maxSpendableAmount == Money.Zero)
            {
                return (Money.Zero, Money.Zero);
            }

            // Create a recipient with a dummy destination address as it's required by NBitcoin's transaction builder.
            List<Recipient> recipients = new[] {new Recipient {Amount = new Money(maxSpendableAmount), ScriptPubKey = new Key().ScriptPubKey}}.ToList();
            Money fee;

            try
            {
                // Here we try to create a transaction that contains all the spendable coins, leaving no room for the fee.
                // When the transaction builder throws an exception informing us that we have insufficient funds, 
                // we use the amount we're missing as the fee.
                var context = new TransactionBuildContext(accountReference, recipients, null)
                {
                    FeeType = feeType,
                    MinConfirmations = allowUnconfirmed ? 0 : 1,
                    TransactionBuilder = new TransactionBuilder()
                };

                this.AddRecipients(context);
                this.AddCoins(context);
                this.AddFee(context);

                // Throw an exception if this code is reached, as building a transaction without any funds for the fee should always throw an exception.
                throw new WalletException("This should be unreachable; please find and fix the bug that caused this to be reached.");
            }
            catch (NotEnoughFundsException e)
            {
                fee = (Money) e.Missing;
            }            

            return (maxSpendableAmount - fee, fee);
        }

        /// <summary>
        /// Load's all the private keys for each of the <see cref="HdAddress"/> in <see cref="TransactionBuildContext.UnspentOutputs"/>
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddSecrets(TransactionBuildContext context)
        {
            if(!context.Sign)
                return;

            Wallet wallet = this.walletManager.GetWalletByName(context.AccountReference.WalletName);

            // get extended private key
            var privateKey = Key.Parse(wallet.EncryptedSeed, context.WalletPassword, wallet.Network);
            var seedExtKey = new ExtKey(privateKey, wallet.ChainCode);

            var signingKeys = new HashSet<ISecret>();
            var added = new HashSet<HdAddress>();
            foreach (var unspentOutputsItem in context.UnspentOutputs.UnspentOutputs)
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

            if (context.UnspentOutputs.UnspentOutputs.Count == 0)
            {
                throw new WalletException($"No spendable transactions found on account {context.AccountReference.AccountName}.");
            }

            // Get total spendable balance in the account.
            var balance = context.UnspentOutputs.UnspentOutputs.Sum(t => t.Transaction.Amount);
            if (balance < context.Recipients.Sum(s => s.Amount))
                throw new WalletException("Not enough funds.");

            if (context.SelectedInputs.Any())
            {
                // 'SelectedInputs' are inputs that must be included in the 
                // current transaction. At this point we check the given 
                // input is part of the UTXO set and filter out UTXOs that are not
                // in the initial list if 'context.AllowOtherInputs' is false.

                var availableHashList = context.UnspentOutputs.UnspentOutputs.ToDictionary(item => item.ToOutPoint(), item => item);

                if(!context.SelectedInputs.All(input => availableHashList.ContainsKey(input)))
                    throw new WalletException($"Not all the inputs in 'SelectedInputs' were found on the wallet.");

                if (!context.AllowOtherInputs)
                {
                    foreach (var unspentOutputsItem in availableHashList)
                        if (!context.SelectedInputs.Contains(unspentOutputsItem.Key))
                            context.UnspentOutputs.UnspentOutputs.Remove(unspentOutputsItem.Value);
                }
            }

            var coins = new List<Coin>();
            foreach (var item in context.UnspentOutputs.UnspentOutputs)
            {
                coins.Add(new Coin(item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey));
            }

            // All the UTXOs are added to the builder without filtering.
            // The builder then has its own coin selection mechanism 
            // to select the best UTXO set for the corresponding amount.
            // To add a custom implementation of a coin selection override 
            // the builder using builder.SetCoinSelection().

            context.TransactionBuilder.AddCoins(coins);
        }

        /// <summary>
        /// Add recipients to the <see cref="TransactionBuilder"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <remarks>
        /// Add outputs to the <see cref="TransactionBuilder"/> based on the <see cref="Recipient"/> list.
        /// </remarks>
        private void AddRecipients(TransactionBuildContext context)
        {
            if (context.Recipients.Any(a => a.Amount == Money.Zero))
                throw new WalletException($"No amount specified");

            if (context.Recipients.Any(a => a.SubtractFeeFromAmount))
                throw new NotImplementedException($"Subtracting the fee from the recipient is not supported yet");

            foreach (var recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        /// <summary>
        /// Use the <see cref="FeeRate"/> from the <see cref="walletFeePolicy"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddFee(TransactionBuildContext context)
        {
            var feeRate = context.OverrideFeeRate ?? this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
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
        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients)
            : this(accountReference, recipients, string.Empty)
        {
        }

        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="accountReference">The wallet and account from which to build this transaction</param>
        /// <param name="recipients">The target recipients to send coins to.</param>
        /// <param name="walletPassword">The password that protects the wallet in <see cref="accountReference"/></param>
        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients, string walletPassword)
        {
            Guard.NotNull(recipients, nameof(recipients));

            this.AccountReference = accountReference;
            this.Recipients = recipients;
            this.WalletPassword = walletPassword;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
            this.SelectedInputs = new List<OutPoint>();
            this.AllowOtherInputs = false;
            this.Sign = !string.IsNullOrEmpty(walletPassword);
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
        /// An indicator to estimate how much fee to spend on a transaction.
        /// </summary>
        /// <remarks>
        /// The higher the fee the faster a transaction will get in to a block. 
        /// </remarks>
        public FeeType FeeType { get; set; }

        /// <summary>
        /// The minimum number of confirmations an output must have to be included as an input.
        /// </summary>
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

        /// <summary>
        /// The inputs that must be used when building the transaction.
        /// </summary>
        /// <remarks>
        /// The inputs are required to be part of the wallet.
        /// </remarks>
        public List<OutPoint> SelectedInputs { get; set; }

        /// <summary>
        /// If false, allows unselected inputs, but requires all selected inputs be used
        /// </summary>
        public bool AllowOtherInputs { get; set; }

        /// <summary>
        /// Specify whether to sign the transaction.
        /// </summary>
        public bool Sign { get; set; }

        /// <summary>
        /// Allows the context to specify a <see cref="FeeRate"/> when building a transaction.
        /// </summary>
        public FeeRate OverrideFeeRate { get; set; }
    }

    /// <summary>
    /// Represents recipients of a payment, used in <see cref="WalletTransactionHandler.BuildTransaction"/> 
    /// </summary>
    public class Recipient
    {
        /// <summary>
        /// The destination script.
        /// </summary>
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The amount that will be sent.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// An indicator if the fee is subtracted from the current recipient.
        /// </summary>
        public bool SubtractFeeFromAmount { get; set; }
    }
}