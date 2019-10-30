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
        Transaction BuildTransaction(TransactionBuildContext context);
    }

    /// <summary>
    /// A builder for building federation transactions.
    /// </summary>
    /// <remarks>
    /// This uses the <see cref="IWalletFeePolicy"/> and the <see cref="TransactionBuilder"/>.
    /// TODO: Move also the broadcast transaction to this class
    /// TODO: Implement lockUnspents
    /// TODO: Implement subtractFeeFromOutputs
    /// </remarks>
    public class FederationWalletTransactionHandler : IFederationWalletTransactionHandler
    {
        public const string NoSpendableTransactionsMessage = "No spendable transactions found.";

        public const string NotEnoughFundsMessage = "Not enough funds.";

        /// <summary>
        /// Amount in satoshis to use as the value for the Op_Return output on the withdrawal transaction.
        /// </summary>
        public const decimal OpReturnSatoshis = 1;

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

        private readonly IFederatedPegSettings settings;

        public FederationWalletTransactionHandler(
            ILoggerFactory loggerFactory,
            IFederationWalletManager walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network,
            IFederatedPegSettings settings)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(settings, nameof(settings));

            this.walletManager = walletManager;
            this.walletFeePolicy = walletFeePolicy;
            this.network = network;
            this.settings = settings;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.privateKeyCache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = new TimeSpan(0, 1, 0) });
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            TransactionBuilder transactionBuilder = this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
            {
                transactionBuilder.Shuffle();
            }

            // build transaction
            Transaction transaction = transactionBuilder.BuildTransaction(context.Sign);

            // If this is a multisig transaction, then by definition we only (usually) possess one of the keys
            // and can therefore not immediately construct a transaction that passes verification
            if (!context.IgnoreVerify)
            {
                if (!transactionBuilder.Verify(transaction, out TransactionPolicyError[] errors))
                {
                    string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
                    this.logger.LogError($"Build transaction failed: {errorsMessage}");
                    throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
                }
            }

            return transaction;
        }

        /// <summary>
        /// Initializes the context transaction builder from information in <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">Transaction build context.</param>
        private TransactionBuilder InitializeTransactionBuilder(Wallet.TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));

            var transactionBuilder = new TransactionBuilder(this.network);

            if (context.IsConsolidatingTransaction)
            {
                transactionBuilder.CoinSelector = new ConsolidationCoinSelector();
            }
            else
            {
                transactionBuilder.CoinSelector = new DeterministicCoinSelector();
            }

            this.AddRecipients(transactionBuilder, context);
            this.AddOpReturnOutput(transactionBuilder, context);
            this.AddCoins(transactionBuilder, context);
            this.AddSecrets(transactionBuilder, context);
            this.FindChangeAddress(transactionBuilder, context);
            this.AddFee(transactionBuilder, context);

            if (context.Time.HasValue)
                transactionBuilder.SetTimeStamp(context.Time.Value);

            return transactionBuilder;
        }

        /// <summary>
        /// Loads the private key for the multisig address.
        /// </summary>
        /// <param name="transactionBuilder"></param>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddSecrets(TransactionBuilder transactionBuilder, TransactionBuildContext context)
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
            transactionBuilder.AddKeys(new[] { privateKey.GetBitcoinSecret(this.network) });
        }

        /// <summary>
        /// Find the next available change address.
        /// </summary>
        /// <param name="transactionBuilder"></param>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void FindChangeAddress(TransactionBuilder transactionBuilder, TransactionBuildContext context)
        {
            // Change address should be the multisig address.
            context.ChangeAddress = this.walletManager.GetWallet().MultiSigAddress;
            transactionBuilder.SetChange(context.ChangeAddress.ScriptPubKey);
        }

        /// <summary>
        /// Find all available outputs (UTXO's) that belong to the multisig address.
        /// Then add them to the <see cref="TransactionBuildContext.UnspentOutputs"/> or <see cref="TransactionBuildContext.UnspentMultiSigOutputs"/>.
        /// </summary>
        /// <param name="transactionBuilder"></param>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddCoins(TransactionBuilder transactionBuilder, TransactionBuildContext context)
        {
            (List<Coin> coins, List<UnspentOutputReference> unspentOutputs) = DetermineCoins(this.walletManager, this.network, context, this.settings);

            context.UnspentOutputs = unspentOutputs;

            if (unspentOutputs.Count == 0)
            {
                throw new WalletException(NoSpendableTransactionsMessage);
            }

            transactionBuilder.AddCoins(coins);
        }

        /// <summary>
        /// Determines the inputs/coins that will be used for the transaction.
        /// </summary>
        /// <param name="walletManager">The federation wallet manager.</param>
        /// <param name="network">The network.</param>
        /// <param name="context">The transacion build context.</param>
        /// <returns>The coins and unspent outputs that will be used.</returns>
        public static (List<Coin>, List<UnspentOutputReference>) DetermineCoins(IFederationWalletManager walletManager, Network network, TransactionBuildContext context, IFederatedPegSettings settings)
        {
            List<UnspentOutputReference> unspentOutputs = walletManager.GetSpendableTransactionsInWallet(context.MinConfirmations).ToList();

            // Get total spendable balance in the account.
            long balance = unspentOutputs.Sum(t => t.Transaction.Amount);
            long totalToSend = context.Recipients.Sum(s => s.Amount);
            if (balance < totalToSend)
                throw new WalletException(NotEnoughFundsMessage);

            if (context.SelectedInputs.Any())
            {
                // 'SelectedInputs' are inputs that must be included in the
                // current transaction. At this point we check the given
                // input is part of the UTXO set and filter out UTXOs that are not
                // in the initial list if 'context.AllowOtherInputs' is false.

                Dictionary<OutPoint, UnspentOutputReference> availableHashList = unspentOutputs.ToDictionary(item => item.ToOutPoint(), item => item);

                if (!context.SelectedInputs.All(input => availableHashList.ContainsKey(input)))
                    throw new WalletException("Not all the selected inputs were found on the wallet.");

                if (!context.AllowOtherInputs)
                {
                    foreach (KeyValuePair<OutPoint, UnspentOutputReference> unspentOutputsItem in availableHashList)
                        if (!context.SelectedInputs.Contains(unspentOutputsItem.Key))
                            unspentOutputs.Remove(unspentOutputsItem.Value);
                }
            }

            long sum = 0;
            int count = 0;
            var coins = new List<Coin>();

            // Assume the outputs came in in-order

            foreach (UnspentOutputReference item in unspentOutputs)
            {
                coins.Add(ScriptCoin.Create(network, item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey, walletManager.GetWallet().MultiSigAddress.RedeemScript));
                sum += item.Transaction.Amount;

                count++;

                // Sufficient UTXOs are selected to cover the value of the outputs + fee. But if it's a consolidating transaction, consume all.
                if (sum >= (totalToSend + settings.GetWithdrawalTransactionFee(count))
                    && !context.IsConsolidatingTransaction)
                    break;
            }

            return (coins, unspentOutputs);
        }

        /// <summary>
        /// Add recipients to the <see cref="TransactionBuilder"/>.
        /// </summary>
        /// <param name="transactionBuilder"></param>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <remarks>
        /// Add outputs to the <see cref="TransactionBuilder"/> based on the <see cref="Recipient"/> list.
        /// </remarks>
        private void AddRecipients(TransactionBuilder transactionBuilder, TransactionBuildContext context)
        {
            if (context.Recipients.Any(a => a.Amount == Money.Zero))
                throw new WalletException("No amount specified.");

            foreach (Recipient recipient in context.Recipients)
                transactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        /// <summary>
        /// Use the <see cref="FeeRate"/> from the <see cref="walletFeePolicy"/>.
        /// </summary>
        /// <param name="transactionBuilder"></param>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddFee(TransactionBuilder transactionBuilder, TransactionBuildContext context)
        {
            Money fee;
            Money minTrxFee = new Money(this.network.MinTxFee, MoneyUnit.Satoshi);

            // If the fee hasn't been set manually, calculate it based on the fee type that was chosen.
            if (context.TransactionFee == null)
            {
                FeeRate feeRate = context.OverrideFeeRate ?? this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
                fee = transactionBuilder.EstimateFees(feeRate);

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

            transactionBuilder.SendFees(fee);
            context.TransactionFee = fee;
        }

        /// <summary>
        /// Add extra unspendable output to the transaction if there is anything in OpReturnData.
        /// </summary>
        /// <param name="transactionBuilder"></param>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddOpReturnOutput(TransactionBuilder transactionBuilder, TransactionBuildContext context)
        {
            if (context.OpReturnData == null) return;

            Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(context.OpReturnData);
            transactionBuilder.Send(opReturnScript, Money.Satoshis(OpReturnSatoshis));
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
        public List<UnspentOutputReference> UnspentOutputs { get; set; }

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

        /// <summary>
        /// The timestamp to set on the transaction.
        /// </summary>
        public uint? Time { get; set; }

        /// <summary>
        /// Whether or not this is a transaction built by the federation to consolidate its own inputs.
        /// </summary>
        public bool IsConsolidatingTransaction { get; set; }
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
        /// We need to reduce the amount being withdrawn by the fees our transaction is going to have.
        /// </summary>
        public Recipient WithPaymentReducedByFee(Money transactionFee)
        {
            Money newAmount = this.Amount - transactionFee;
            return new Recipient
            {
                Amount = newAmount,
                ScriptPubKey = this.ScriptPubKey
            };
        }
    }
}
