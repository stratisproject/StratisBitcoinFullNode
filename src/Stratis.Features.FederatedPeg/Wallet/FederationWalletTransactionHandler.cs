using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.Wallet
{
    public interface IFederationWalletTransactionHandler
    {
        /// <summary>
        /// Builds a new transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(Wallet.TransactionBuildContext context);
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
    public class FederationWalletTransactionHandler : IFederationWalletTransactionHandler
    {
        /// <summary>A threshold that if possible will limit the amount of UTXO sent to the <see cref="ICoinSelector"/>.</summary>
        /// <remarks>
        /// 500 is a safe number that if reached ensures the coin selector will not take too long to complete,
        /// most regular wallets will never reach such a high number of UTXO.
        /// </remarks>
        private const int SendCountThresholdLimit = 500;

        private readonly IFederationWalletManager walletManager;

        private readonly IWalletFeePolicy walletFeePolicy;

        private readonly ILogger logger;

        private readonly Network network;

        private readonly MemoryCache privateKeyCache;

        public FederationWalletTransactionHandler(
            ILoggerFactory loggerFactory,
            IFederationWalletManager walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(network, nameof(network));

            this.walletManager = walletManager;
            this.walletFeePolicy = walletFeePolicy;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.privateKeyCache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = new TimeSpan(0, 1, 0) });
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
            {
                context.TransactionBuilder.Shuffle();
            }

            // build transaction
            context.Transaction = context.TransactionBuilder.BuildTransaction(context.Sign);

            // If this is a multisig transaction, then by definition we only (usually) possess one of the keys
            // and can therefore not immediately construct a transaction that passes verification
            if (!context.IgnoreVerify)
            {
                if (!context.TransactionBuilder.Verify(context.Transaction, out TransactionPolicyError[] errors))
                {
                    string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
                    this.logger.LogError($"Build transaction failed: {errorsMessage}");
                    throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
                }
            }

            return context.Transaction;
        }

        /// <summary>
        /// Initializes the context transaction builder from information in <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">Transaction build context.</param>
        private void InitializeTransactionBuilder(Wallet.TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));

            context.TransactionBuilder = new TransactionBuilder(this.network);

            this.AddRecipients(context);
            this.AddOpReturnOutput(context);
            this.AddCoins(context);
            this.AddSecrets(context);
            this.FindChangeAddress(context);
            this.AddFee(context);
        }

        /// <summary>
        /// Loads the private key for the multisig address.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddSecrets(TransactionBuildContext context)
        {
            if (!context.Sign)
                return;

            FederationWallet wallet = this.walletManager.GetWallet();

            // Get the encrypted private key.
            string cacheKey = wallet.EncryptedSeed;

            Key privateKey;

            // Check if the private key is in the cache.
            if (this.privateKeyCache.TryGetValue(cacheKey, out SecureString secretValue))
            {
                privateKey = wallet.Network.CreateBitcoinSecret(secretValue.FromSecureString()).PrivateKey;
                this.privateKeyCache.Set(cacheKey, secretValue, new TimeSpan(0, 5, 0));
            }
            else
            {
                privateKey = Key.Parse(wallet.EncryptedSeed, context.WalletPassword, wallet.Network);
                this.privateKeyCache.Set(cacheKey, privateKey.ToString(wallet.Network).ToSecureString(), new TimeSpan(0, 5, 0));
            }

            // Add the key used to sign the output.
            context.TransactionBuilder.AddKeys(new[] { privateKey.GetBitcoinSecret(this.network) });
        }

        /// <summary>
        /// Compares transaction data to determine the order of inclusion in the transaction.
        /// </summary>
        /// <param name="x">First transaction data.</param>
        /// <param name="y">Second transaction data.</param>
        /// <returns>Returns <c>0</c> if the outputs are the same and <c>-1<c> or <c>1</c> depending on whether the first or second output takes precedence.</returns>
        public static int CompareTransactionData(TransactionData x, TransactionData y)
        {
            // The oldest UTXO (determined by block height) is selected first.
            if ((x.BlockHeight ?? int.MaxValue) != (y.BlockHeight ?? int.MaxValue))
            {
                return ((x.BlockHeight ?? int.MaxValue) < (y.BlockHeight ?? int.MaxValue)) ? -1 : 1;
            }

            // If a block has more than one UTXO, then they are selected in order of transaction id.
            if (x.Id != y.Id)
            {
                return (x.Id < y.Id) ? -1 : 1;
            }

            // If multiple UTXOs appear within a transaction then they are selected in ascending index order.
            if (x.Index != y.Index)
            {
                return (x.Index < y.Index) ? -1 : 1;
            }

            return 0;
        }

        /// <summary>
        /// Compares two unspent outputs to determine the order of inclusion in the transaction.
        /// </summary>
        /// <param name="x">First unspent output.</param>
        /// <param name="y">Second unspent output.</param>
        /// <returns>Returns <c>0</c> if the outputs are the same and <c>-1<c> or <c>1</c> depending on whether the first or second output takes precedence.</returns>
        private int CompareUnspentOutputReferences(UnspentOutputReference x, UnspentOutputReference y)
        {
            return CompareTransactionData(x.Transaction, y.Transaction);
        }

        /// <summary>
        /// Returns the unspent outputs in the preferred order of consumption.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <returns>The unspent outputs in the preferred order of consumption.</returns>
        private IOrderedEnumerable<UnspentOutputReference> GetOrderedUnspentOutputs(TransactionBuildContext context)
        {
            return context.UnspentOutputs.OrderBy(a => a, Comparer<UnspentOutputReference>.Create((x, y) => this.CompareUnspentOutputReferences(x, y)));
        }

        /// <summary>
        /// Find the next available change address.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void FindChangeAddress(TransactionBuildContext context)
        {
            // Change address should be the multisig address.
            context.ChangeAddress = this.walletManager.GetWallet().MultiSigAddress;
            context.TransactionBuilder.SetChange(context.ChangeAddress.ScriptPubKey);
        }

        /// <summary>
        /// Find all available outputs (UTXO's) that belong to the multisig address.
        /// Then add them to the <see cref="TransactionBuildContext.UnspentOutputs"/> or <see cref="TransactionBuildContext.UnspentMultiSigOutputs"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddCoins(TransactionBuildContext context)
        {
            context.UnspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(context.MinConfirmations).ToList();

            if (context.UnspentOutputs.Count == 0)
            {
                throw new WalletException("No spendable transactions found.");
            }

            // Get total spendable balance in the account.
            long balance = context.UnspentOutputs.Sum(t => t.Transaction.Amount);
            long totalToSend = context.Recipients.Sum(s => s.Amount);
            if (balance < totalToSend)
                throw new WalletException("Not enough funds.");

            if (context.SelectedInputs.Any())
            {
                // 'SelectedInputs' are inputs that must be included in the
                // current transaction. At this point we check the given
                // input is part of the UTXO set and filter out UTXOs that are not
                // in the initial list if 'context.AllowOtherInputs' is false.

                Dictionary<OutPoint, UnspentOutputReference> availableHashList = context.UnspentOutputs.ToDictionary(item => item.ToOutPoint(), item => item);

                if (!context.SelectedInputs.All(input => availableHashList.ContainsKey(input)))
                    throw new WalletException("Not all the selected inputs were found on the wallet.");

                if (!context.AllowOtherInputs)
                {
                    foreach (KeyValuePair<OutPoint, UnspentOutputReference> unspentOutputsItem in availableHashList)
                        if (!context.SelectedInputs.Contains(unspentOutputsItem.Key))
                            context.UnspentOutputs.Remove(unspentOutputsItem.Value);
                }
            }

            long sum = 0;
            int index = 0;
            var coins = new List<Coin>();

            foreach (UnspentOutputReference item in context.OrderCoinsDeterministic ?
                this.GetOrderedUnspentOutputs(context) : context.UnspentOutputs.OrderByDescending(a => a.Transaction.Amount))
            {
                coins.Add(ScriptCoin.Create(this.network, item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey, this.walletManager.GetWallet().MultiSigAddress.RedeemScript));
                sum += item.Transaction.Amount;
                index++;

                // Sufficient UTXOs are selected to cover the value of the outputs + fee.
                if (sum >= (totalToSend + context.TransactionFee))
                    break;

            }

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
                throw new WalletException("No amount specified.");

            if (context.Recipients.Any(a => a.SubtractFeeFromAmount))
                throw new NotImplementedException("Substracting the fee from the recipient is not supported yet.");

            foreach (Recipient recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        /// <summary>
        /// Use the <see cref="FeeRate"/> from the <see cref="walletFeePolicy"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddFee(TransactionBuildContext context)
        {
            Money fee;
            Money minTrxFee = new Money(this.network.MinTxFee, MoneyUnit.Satoshi);

            // If the fee hasn't been set manually, calculate it based on the fee type that was chosen.
            if (context.TransactionFee == null)
            {
                FeeRate feeRate = context.OverrideFeeRate ?? this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
                fee = context.TransactionBuilder.EstimateFees(feeRate);

                // Make sure that the fee is at least the minimum transaction fee.
                fee = Math.Max(fee, minTrxFee);
            }
            else
            {
                if (context.TransactionFee < minTrxFee)
                {
                    throw new WalletException($"Not enough fees. The minimum fee is {minTrxFee}.");
                }

                fee = context.TransactionFee;
            }

            context.TransactionBuilder.SendFees(fee);
            context.TransactionFee = fee;
        }

        /// <summary>
        /// Add extra unspendable output to the transaction if there is anything in OpReturnData.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddOpReturnOutput(TransactionBuildContext context)
        {
            if (context.OpReturnData == null) return;

            Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(context.OpReturnData);
            context.TransactionBuilder.Send(opReturnScript, Money.Zero);
        }
    }

    public class TransactionBuildContext
    {
        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="recipients">The target recipients to send coins to.</param>
        public TransactionBuildContext(List<Recipient> recipients) : this(recipients, string.Empty)
        {
        }

        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="recipients">The target recipients to send coins to.</param>
        /// <param name="walletPassword">The password that protects the member's seed.</param>
        public TransactionBuildContext(List<Recipient> recipients, string walletPassword = "", byte[] opReturnData = null)
        {
            Guard.NotNull(recipients, nameof(recipients));

            this.Recipients = recipients;
            this.WalletPassword = walletPassword;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
            this.SelectedInputs = new List<OutPoint>();
            this.AllowOtherInputs = false;
            this.Sign = !string.IsNullOrEmpty(walletPassword);
            this.OpReturnData = opReturnData;
            this.MultiSig = null;
            this.IgnoreVerify = false;
        }

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
        public List<Wallet.UnspentOutputReference> UnspentOutputs { get; set; }

        public Network Network { get; set; }

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
        public MultiSigAddress ChangeAddress { get; set; }

        /// <summary>
        /// The total fee on the transaction.
        /// </summary>
        public Money TransactionFee { get; set; }

        /// <summary>
        /// The final transaction.
        /// </summary>
        public Transaction Transaction { get; set; }

        /// <summary>
        /// The password that protects the member's seed.
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
        /// If <c>true</c> coins will be ordered using (block height + transaction id + output index) ordering.
        /// </summary>
        public bool OrderCoinsDeterministic { get; set; }

        /// <summary>
        /// Specify whether to sign the transaction.
        /// </summary>
        public bool Sign { get; set; }

        /// <summary>
        /// Allows the context to specify a <see cref="FeeRate"/> when building a transaction.
        /// </summary>
        public FeeRate OverrideFeeRate { get; set; }

        /// <summary>
        /// Shuffles transaction inputs and outputs for increased privacy.
        /// </summary>
        public bool Shuffle { get; set; }

        /// <summary>
        /// Optional data to be added as an extra OP_RETURN transaction output with Money.Zero value.
        /// </summary>
        public byte[] OpReturnData { get; set; }

        /// <summary>
        /// If not null, indicates the multisig address details that funds can be sourced from.
        /// </summary>
        public MultiSigAddress MultiSig { get; set; }

        /// <summary>
        /// If true, do not perform verification on the built transaction (e.g. it is partially signed)
        /// </summary>
        public bool IgnoreVerify { get; set; }
    }

    /// <summary>
    /// Represents recipients of a payment, used in <see cref="FederationWalletTransactionHandler.BuildTransaction"/>
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
