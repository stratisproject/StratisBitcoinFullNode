using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Events;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Payloads;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.InputConsolidation
{
    public class InputConsolidator : IInputConsolidator
    {
        public static readonly Money ConsolidationFee = Money.Coins(0.005m); // 50 inputs. This is roughly half the same as fee on withdrawals

        private readonly IFederatedPegBroadcaster federatedPegBroadcaster;
        private readonly IFederationWalletTransactionHandler transactionHandler;
        private readonly IFederationWalletManager walletManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IFederatedPegSettings settings;
        private readonly ILogger logger;
        private readonly Network network;

        /// <summary>
        /// Used to ensure only one operation is happening at a time.
        /// </summary>
        private readonly object lockObj = new object();

        /// <inheritdoc />
        public List<ConsolidationTransaction> ConsolidationTransactions { get; private set; }

        // TODO: Do we need a dictionary here? ^^

        public InputConsolidator(IFederatedPegBroadcaster federatedPegBroadcaster,
            IFederationWalletTransactionHandler transactionHandler,
            IFederationWalletManager walletManager,
            IBroadcasterManager broadcasterManager,
            IFederatedPegSettings settings,
            ILoggerFactory loggerFactory,
            ISignals signals,
            Network network)
        {
            this.federatedPegBroadcaster = federatedPegBroadcaster;
            this.transactionHandler = transactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.network = network;
            this.settings = settings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            signals.Subscribe<WalletNeedsConsolidation>(this.StartConsolidation);
        }

        /// <inheritdoc />
        public void StartConsolidation(WalletNeedsConsolidation trigger)
        {
            Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    // If we're already in progress we don't need to do anything.
                    if (this.ConsolidationTransactions != null)
                        return;

                    this.logger.LogInformation("Building consolidation transactions for federation wallet inputs.");

                    this.ConsolidationTransactions = this.CreateRequiredConsolidationTransactions(trigger.Amount);

                    if (this.ConsolidationTransactions == null)
                        return;

                    this.logger.LogInformation("Successfully built {0} consolidating transactions.", this.ConsolidationTransactions.Count);
                }
            });
        }

        /// <inheritdoc />
        public ConsolidationSignatureResult CombineSignatures(Transaction incomingPartialTransaction)
        {
            lock (this.lockObj)
            {
                // Nothing to sign.
                if (this.ConsolidationTransactions == null)
                    return ConsolidationSignatureResult.Failed();

                // Get matching in-memory transaction.
                ConsolidationTransaction inMemoryTransaction = this.GetInMemoryConsolidationTransaction(incomingPartialTransaction);

                // Transaction doesn't exist or need signing.
                if (inMemoryTransaction == null || inMemoryTransaction.Status != CrossChainTransferStatus.Partial)
                    return ConsolidationSignatureResult.Failed();

                // Attempt to merge signatures
                var builder = new TransactionBuilder(this.network);
                Transaction oldTransaction = inMemoryTransaction.PartialTransaction;

                this.logger.LogDebug("Attempting to merge signatures for {0} and {1}.", inMemoryTransaction.PartialTransaction.GetHash(), incomingPartialTransaction.GetHash());

                Transaction newTransaction = SigningUtils.CombineSignatures(builder, inMemoryTransaction.PartialTransaction, new []{incomingPartialTransaction});

                if (oldTransaction.GetHash() == newTransaction.GetHash())
                {
                    // Signing didn't work if the hash is still the same
                    this.logger.LogDebug("Signing failed.");
                    return ConsolidationSignatureResult.Failed();
                }

                this.logger.LogDebug("Successfully signed transaction.");
                inMemoryTransaction.PartialTransaction = newTransaction;

                // NOTE: We don't need to reserve the transaction. The wallet will be at a standstill whilst this is happening.

                // If it is FullySigned, broadcast.
                if (this.walletManager.ValidateConsolidatingTransaction(inMemoryTransaction.PartialTransaction, true))
                {
                    inMemoryTransaction.Status = CrossChainTransferStatus.FullySigned;
                    this.logger.LogDebug("Consolidation transaction is fully signed. Broadcasting {0}", inMemoryTransaction.PartialTransaction.GetHash());
                    this.broadcasterManager.BroadcastTransactionAsync(inMemoryTransaction.PartialTransaction);
                }

                this.logger.LogDebug("Consolidation transaction not fully signed yet.");

                return ConsolidationSignatureResult.Succeeded(inMemoryTransaction.PartialTransaction);
            }
        }

        public List<ConsolidationTransaction> CreateRequiredConsolidationTransactions(Money amount)
        {
            // Get all of the inputs
            List<UnspentOutputReference> unspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(WithdrawalTransactionBuilder.MinConfirmations).ToList();

            // We shouldn't be consolidating transactions if we have less than 50 UTXOs to spend.
            if (unspentOutputs.Count < WithdrawalTransactionBuilder.MaxInputs)
                return null;

            // Go through every set of 50 until we consume all, or find a set that works.
            List<UnspentOutputReference> oneRound = unspentOutputs.Take(WithdrawalTransactionBuilder.MaxInputs).ToList();
            int roundNumber = 0;

            List<ConsolidationTransaction> consolidationTransactions = new List<ConsolidationTransaction>();
            
            while (oneRound.Count == WithdrawalTransactionBuilder.MaxInputs)
            {
                // We found a set of 50 that is worth enough so no more consolidation needed.
                if (oneRound.Sum(x => x.Transaction.Amount) >= amount + this.settings.GetWithdrawalTransactionFee(WithdrawalTransactionBuilder.MaxInputs))
                    break;

                // build a transaction and add it to our list.
                Transaction transaction = this.BuildConsolidatingTransaction(oneRound);

                // Something went wrong building transaction - start over. We will want to build them all from scratch in case wallet has changed state.
                if (transaction == null)
                    return null;

                consolidationTransactions.Add(new ConsolidationTransaction
                {
                    PartialTransaction = transaction,
                    Status = CrossChainTransferStatus.Partial
                });

                roundNumber++;
                oneRound = unspentOutputs
                    .Skip(roundNumber * WithdrawalTransactionBuilder.MaxInputs)
                    .Take(WithdrawalTransactionBuilder.MaxInputs).ToList();
            }

            // Loop exits when we get a set of 50 that had a high enough amount, or when we run out of UTXOs aka a round less than 50

            return consolidationTransactions;
        }

        /// <summary>
        /// Build a consolidating transaction.
        /// </summary>
        public Transaction BuildConsolidatingTransaction(List<UnspentOutputReference> selectedInputs)
        {
            try
            {
                string walletPassword = this.walletManager.Secret.WalletPassword;
                bool sign = (walletPassword ?? "") != "";

                var multiSigContext = new TransactionBuildContext(new List<Recipient>())
                {
                    MinConfirmations = WithdrawalTransactionBuilder.MinConfirmations,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = sign,
                    TransactionFee = ConsolidationFee,
                    SelectedInputs = selectedInputs.Select(u => u.ToOutPoint()).ToList(),
                    AllowOtherInputs = false,
                    IsConsolidatingTransaction = true,
                    Time = (uint?) selectedInputs.First().Transaction.CreationTime.ToUnixTimeSeconds()
                };

                Transaction transaction = this.transactionHandler.BuildTransaction(multiSigContext);

                this.logger.LogDebug("Consolidating transaction = {0}", transaction.ToString(this.network, RawFormat.BlockExplorer));

                return transaction;
            }
            catch (Exception e)
            {
                this.logger.LogWarning("Exception when building consolidating transaction. Wallet state likely changed before calling: " + e);
                return null;
            }
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction)
        {
            lock (this.lockObj)
            {
                // No work to do
                if (this.ConsolidationTransactions == null)
                    return;

                // If we have a transaction in memory that is not FullySigned yet, set it to be as such when we receive a transaction from the mempool.
                if (this.IsConsolidatingTransaction(transaction))
                {
                    ConsolidationTransaction inMemoryTransaction = this.GetInMemoryConsolidationTransaction(transaction);

                    if (inMemoryTransaction != null && inMemoryTransaction.Status == CrossChainTransferStatus.Partial)
                    {
                        this.logger.LogDebug("Saw condensing transaction {0} in mempool, updating its status to FullySigned", transaction.GetHash());
                        inMemoryTransaction.Status = CrossChainTransferStatus.FullySigned;
                        inMemoryTransaction.PartialTransaction = transaction;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            lock (this.lockObj)
            {
                // No work to do
                if (this.ConsolidationTransactions == null)
                    return;

                // If a consolidation transaction comes through, set it to SeenInBlock
                foreach (Transaction transaction in chainedHeaderBlock.Block.Transactions)
                {
                    if (this.IsConsolidatingTransaction(transaction))
                    {
                        ConsolidationTransaction inMemoryTransaction = this.GetInMemoryConsolidationTransaction(transaction);

                        if (inMemoryTransaction != null)
                        {
                            this.logger.LogDebug("Saw condensing transaction {0}, updating status to SeenInBlock", transaction.GetHash());
                            inMemoryTransaction.Status = CrossChainTransferStatus.SeenInBlock;
                        }
                    }
                }

                // Need to check all the transactions that are partial are still valid in case of a reorg.
                List<ConsolidationTransaction> partials = this.ConsolidationTransactions
                    .Where(x=>x.Status == CrossChainTransferStatus.Partial || x.Status == CrossChainTransferStatus.FullySigned)
                    .Take(5) // We don't actually need to validate all of them - just the next potential ones.
                    .ToList();

                foreach (ConsolidationTransaction cTransaction in partials)
                {
                    if (!this.walletManager.ValidateConsolidatingTransaction(cTransaction.PartialTransaction))
                    {
                        // If we find an invalid one, everything will need redoing!
                        this.logger.LogDebug("Consolidation transaction {0} failed validation, resetting InputConsolidator", cTransaction.PartialTransaction.GetHash());
                        this.ConsolidationTransactions = null;
                        return;
                    }
                }

                // If all of our consolidation inputs are SeenInBlock, we can move on! Yay
                if (this.ConsolidationTransactions.All(x => x.Status == CrossChainTransferStatus.SeenInBlock))
                    this.ConsolidationTransactions = null;
            }
        }

        private bool IsConsolidatingTransaction(Transaction transaction)
        {
            return transaction.Inputs.Count == WithdrawalTransactionBuilder.MaxInputs
                   && transaction.Outputs.Count == 1
                   && transaction.Outputs[0].ScriptPubKey == this.settings.MultiSigAddress.ScriptPubKey;
        }

        private ConsolidationTransaction GetInMemoryConsolidationTransaction(Transaction toMatch)
        {
            TxIn toMatchInput = toMatch.Inputs[0];
            return this.ConsolidationTransactions.FirstOrDefault(x => x.PartialTransaction.Inputs[0].PrevOut == toMatchInput.PrevOut);
        }
    }
}
