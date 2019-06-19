using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Events;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.InputConsolidation
{
    public class InputConsolidator : IInputConsolidator
    {
        private readonly IFederationWalletTransactionHandler transactionHandler;
        private readonly IFederationWalletManager walletManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IFederatedPegSettings settings;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IAsyncProvider asyncProvider;

        /// <summary>
        /// Queue to handle incoming blocks and update state if needed.
        /// </summary>
        private readonly IAsyncDelegateDequeuer<ChainedHeaderBlock> blockQueue;

        /// <summary>
        /// The task that is building all the consolidation transactions, if one is currently running.
        /// </summary>
        /// <remarks>
        /// Protected by <see cref="taskLock"/>.
        /// </remarks>
        private Task consolidationTask;

        /// <summary>
        /// Used to protect <see cref="ConsolidationTransactions"/> from write operations.
        /// </summary>
        private readonly object txLock = new object();

        /// <summary>
        /// Used to protect <see cref="consolidationTask"/> from write operations.
        /// </summary>
        private readonly object taskLock = new object();

        /// <inheritdoc />
        /// <remarks>
        /// Protected by <see cref="txLock"/>.
        /// </remarks>
        public List<ConsolidationTransaction> ConsolidationTransactions { get; private set; }

        // TODO: Could put a dictionary by OutPoint.

        public InputConsolidator(IFederationWalletTransactionHandler transactionHandler,
            IFederationWalletManager walletManager,
            IBroadcasterManager broadcasterManager,
            IFederatedPegSettings settings,
            ILoggerFactory loggerFactory,
            ISignals signals,
            IAsyncProvider asyncProvider,
            Network network)
        {
            this.transactionHandler = transactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.network = network;
            this.settings = settings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.asyncProvider = asyncProvider;
            this.blockQueue = asyncProvider.CreateAndRunAsyncDelegateDequeuer<ChainedHeaderBlock>($"{nameof(InputConsolidator)}-{nameof(this.blockQueue)}", this.ProcessBlockInternal);
            signals.Subscribe<WalletNeedsConsolidation>(this.StartConsolidation);
        }

        /// <inheritdoc />
        public void StartConsolidation(WalletNeedsConsolidation trigger)
        {
            // If there isn't already a task running, start it.
            lock (this.taskLock)
            {
                if (this.consolidationTask == null || this.consolidationTask.IsCompleted)
                {
                    this.consolidationTask = Task.Run(() =>
                    {
                        lock (this.txLock)
                        {
                            // If we're already in progress we don't need to do anything.
                            if (this.ConsolidationTransactions != null)
                                return;

                            this.logger.LogInformation("Building consolidation transactions for federation wallet inputs.");

                            this.ConsolidationTransactions = this.CreateRequiredConsolidationTransactions(trigger.Amount);

                            if (this.ConsolidationTransactions == null)
                            {
                                this.logger.LogWarning("Failed to build condensing transactions.");
                                return;
                            }

                            this.logger.LogInformation("Successfully built {0} consolidating transactions.", this.ConsolidationTransactions.Count);
                        }
                    });

                    this.asyncProvider.RegisterTask($"{nameof(InputConsolidator)}-transaction building", this.consolidationTask);
                }
            }
        }

        /// <inheritdoc />
        public ConsolidationSignatureResult CombineSignatures(Transaction incomingPartialTransaction)
        {
            lock (this.txLock)
            {
                // Nothing to sign.
                if (this.ConsolidationTransactions == null)
                    return ConsolidationSignatureResult.Failed();

                // Get matching in-memory transaction.
                ConsolidationTransaction inMemoryTransaction = this.GetInMemoryConsolidationTransaction(incomingPartialTransaction);

                // Transaction doesn't exist or need signing.
                if (inMemoryTransaction == null || inMemoryTransaction.Status != ConsolidationTransactionStatus.Partial)
                    return ConsolidationSignatureResult.Failed();

                // Attempt to merge signatures
                var builder = new TransactionBuilder(this.network);
                Transaction oldTransaction = inMemoryTransaction.PartialTransaction;

                this.logger.LogDebug("Attempting to merge signatures for '{0}' and '{1}'.", inMemoryTransaction.PartialTransaction.GetHash(), incomingPartialTransaction.GetHash());

                Transaction newTransaction = SigningUtils.CheckTemplateAndCombineSignatures(builder, inMemoryTransaction.PartialTransaction, new []{incomingPartialTransaction});

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
                    inMemoryTransaction.Status = ConsolidationTransactionStatus.FullySigned;
                    this.logger.LogDebug("Consolidation transaction is fully signed. Broadcasting '{0}'", inMemoryTransaction.PartialTransaction.GetHash());
                    this.broadcasterManager.BroadcastTransactionAsync(inMemoryTransaction.PartialTransaction).GetAwaiter().GetResult();
                    return ConsolidationSignatureResult.Succeeded(inMemoryTransaction.PartialTransaction);
                }

                this.logger.LogDebug("Consolidation transaction not fully signed yet.");

                return ConsolidationSignatureResult.Succeeded(inMemoryTransaction.PartialTransaction);
            }
        }

        /// <summary>
        /// Builds a list of consolidation transactions that will need to pass before the next withdrawal transaction can come through.
        /// </summary>
        public List<ConsolidationTransaction> CreateRequiredConsolidationTransactions(Money amount)
        {
            // TODO: This method doesn't need to be public.

            lock (this.txLock)
            {
                // Get all of the inputs
                List<UnspentOutputReference> unspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(WithdrawalTransactionBuilder.MinConfirmations).ToList();

                // We shouldn't be consolidating transactions if we have less than 50 UTXOs to spend.
                if (unspentOutputs.Count < FederatedPegSettings.MaxInputs)
                {
                    this.logger.LogDebug("Not enough UTXOs to trigger consolidation transactions.");
                    return null;
                }

                // Go through every set of 50 until we consume all, or find a set that works.
                List<UnspentOutputReference> oneRound = unspentOutputs.Take(FederatedPegSettings.MaxInputs).ToList();
                int roundNumber = 0;

                List<ConsolidationTransaction> consolidationTransactions = new List<ConsolidationTransaction>();

                while (oneRound.Count == FederatedPegSettings.MaxInputs)
                {
                    // We found a set of 50 that is worth enough so no more consolidation needed.
                    if (oneRound.Sum(x => x.Transaction.Amount) >= amount + this.settings.GetWithdrawalTransactionFee(FederatedPegSettings.MaxInputs))
                        break;

                    // build a transaction and add it to our list.
                    Transaction transaction = this.BuildConsolidatingTransaction(oneRound);

                    // Something went wrong building transaction - start over. We will want to build them all from scratch in case wallet has changed state.
                    if (transaction == null)
                    {
                        this.logger.LogDebug("Failure building specific consolidating transaction.");
                        return null;
                    }

                    consolidationTransactions.Add(new ConsolidationTransaction
                    {
                        PartialTransaction = transaction,
                        Status = ConsolidationTransactionStatus.Partial
                    });

                    roundNumber++;
                    oneRound = unspentOutputs
                        .Skip(roundNumber * FederatedPegSettings.MaxInputs)
                        .Take(FederatedPegSettings.MaxInputs).ToList();
                }

                // Loop exits when we get a set of 50 that had a high enough amount, or when we run out of UTXOs aka a round less than 50

                return consolidationTransactions;
            }
        }

        /// <summary>
        /// Build a consolidating transaction.
        /// </summary>
        private Transaction BuildConsolidatingTransaction(List<UnspentOutputReference> selectedInputs)
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
                    TransactionFee = FederatedPegSettings.ConsolidationFee,
                    SelectedInputs = selectedInputs.Select(u => u.ToOutPoint()).ToList(),
                    AllowOtherInputs = false,
                    IsConsolidatingTransaction = true,
                    Time = this.network.Consensus.IsProofOfStake 
                        ? (uint?)selectedInputs.Max(x => x.Transaction.CreationTime).ToUnixTimeSeconds()
                        : null
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
            // TODO: It would be nice if there was a way to quickly check that the transaction coming in through here is FullySigned.
            // We can check the number of signatures for a start.
            // At the moment we know they are only coming from the mempool.

            // TODO: Could also be async to avoid blocking other components receiving future transaction details

            lock (this.txLock)
            {
                // No work to do
                if (this.ConsolidationTransactions == null)
                    return;

                // If we have a transaction in memory that is not FullySigned yet, set it to be as such when we receive a transaction from the mempool.
                if (this.IsConsolidatingTransaction(transaction))
                {
                    ConsolidationTransaction inMemoryTransaction = this.GetInMemoryConsolidationTransaction(transaction);

                    if (inMemoryTransaction != null && inMemoryTransaction.Status == ConsolidationTransactionStatus.Partial)
                    {
                        this.logger.LogDebug("Saw consolidating transaction {0} in mempool, updating its status to FullySigned", transaction.GetHash());
                        inMemoryTransaction.Status = ConsolidationTransactionStatus.FullySigned;
                        inMemoryTransaction.PartialTransaction = transaction;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.blockQueue.Enqueue(chainedHeaderBlock);
        }

        private Task ProcessBlockInternal(ChainedHeaderBlock chainedHeaderBlock, CancellationToken cancellationToken)
        {
            lock (this.txLock)
            {
                // No work to do
                if (this.ConsolidationTransactions == null)
                    return Task.CompletedTask;

                // If a consolidation transaction comes through, set it to SeenInBlock
                foreach (Transaction transaction in chainedHeaderBlock.Block.Transactions)
                {
                    if (this.IsConsolidatingTransaction(transaction))
                    {
                        ConsolidationTransaction inMemoryTransaction =
                            this.GetInMemoryConsolidationTransaction(transaction);

                        if (inMemoryTransaction != null)
                        {
                            this.logger.LogDebug("Saw condensing transaction {0}, updating status to SeenInBlock",
                                transaction.GetHash());
                            inMemoryTransaction.Status = ConsolidationTransactionStatus.SeenInBlock;
                        }
                    }
                }

                // Need to check all the transactions that are partial are still valid in case of a reorg.
                List<ConsolidationTransaction> partials = this.ConsolidationTransactions
                    .Where(x => x.Status == ConsolidationTransactionStatus.Partial ||
                                x.Status == ConsolidationTransactionStatus.FullySigned)
                    .Take(5) // We don't actually need to validate all of them - just the next potential ones.
                    .ToList();

                foreach (ConsolidationTransaction cTransaction in partials)
                {
                    if (!this.walletManager.ValidateConsolidatingTransaction(cTransaction.PartialTransaction))
                    {
                        // If we find an invalid one, everything will need redoing!
                        this.logger.LogDebug(
                            "Consolidation transaction {0} failed validation, resetting InputConsolidator",
                            cTransaction.PartialTransaction.GetHash());
                        this.ConsolidationTransactions = null;
                        return Task.CompletedTask;
                    }
                }

                // If all of our consolidation inputs are SeenInBlock, we can move on! Yay
                if (this.ConsolidationTransactions.All(x => x.Status == ConsolidationTransactionStatus.SeenInBlock))
                    this.ConsolidationTransactions = null;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Discerns whether an incoming transaction is a consolidating transaction.
        /// </summary>
        private bool IsConsolidatingTransaction(Transaction transaction)
        {
            return transaction.Inputs.Count == FederatedPegSettings.MaxInputs
                   && transaction.Outputs.Count == 1
                   && transaction.Outputs[0].ScriptPubKey == this.settings.MultiSigAddress.ScriptPubKey;
        }

        /// <summary>
        /// Gets the equivalent transaction on this node for any incoming transaction.
        /// </summary>
        private ConsolidationTransaction GetInMemoryConsolidationTransaction(Transaction toMatch)
        {
            if (toMatch?.Inputs == null || !toMatch.Inputs.Any())
                return null;

            TxIn toMatchInput = toMatch.Inputs[0];
            return this.ConsolidationTransactions.FirstOrDefault(x => x.PartialTransaction.Inputs[0].PrevOut == toMatchInput.PrevOut);
        }
    }
}
