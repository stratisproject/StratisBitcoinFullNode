using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Events;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Payloads;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class InputConsolidator : IInputConsolidator
    {
        public static readonly Money ConsolidationFee = Money.Coins(0.0025m); // 50 inputs. This is roughly half the withdrawal fee. TODO: Consider this number

        private readonly IFederatedPegBroadcaster federatedPegBroadcaster;
        private readonly IFederationWalletTransactionHandler transactionHandler;
        private readonly IFederationWalletManager walletManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IFederatedPegSettings settings;
        private readonly ISignals signals;
        private readonly ILogger logger;
        private readonly Network network;

        private bool signingInProgress;

        private bool fullySigned;

        /// <summary>
        /// Used to ensure only one operation is happening at a time.
        /// </summary>
        private readonly object lockObj = new object();

        /// <inheritdoc />
        public Transaction PartialTransaction { get; private set; }

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
            lock (this.lockObj)
            {
                // Has done it's work and broadcasted the tx to mempool. Just need to be patient now.
                if (this.signingInProgress && this.fullySigned)
                    return;

                // Re-trigger signing rounds if we're hitting this a second time and we're not fully signed yet.
                if (this.signingInProgress)
                {
                    this.BroadcastPartial();
                    return;
                }

                this.logger.LogInformation("Building consolidation transaction for federation wallet inputs.");

                // Build condensing transaction in deterministic way
                this.PartialTransaction = this.BuildConsolidatingTransaction();

                // Something went wrong building the transaction.
                if (this.PartialTransaction == null)
                    return;

                this.signingInProgress = true;

                // Send it around to be signed
                this.BroadcastPartial();
            }
        }

        /// <summary>
        /// Broadcast our partial to be signed by the other federation nodes.
        /// </summary>
        private void BroadcastPartial()
        {
            this.logger.LogDebug("Broadcasting partial consolidation transaction to federation.");
            RequestPartialTransactionPayload payload = new RequestPartialTransactionPayload(RequestPartialTransactionPayload.ConsolidationDepositId).AddPartial(this.PartialTransaction);
            this.federatedPegBroadcaster.BroadcastAsync(payload).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public ConsolidationSignatureResult CombineSignatures(Transaction incomingPartialTransaction)
        {
            lock (this.lockObj)
            {
                // No need to sign in these cases.
                if (!this.signingInProgress || this.fullySigned)
                    return ConsolidationSignatureResult.Failed();

                // Attempt to merge signatures
                var builder = new TransactionBuilder(this.network);
                Transaction oldTransaction = this.PartialTransaction;

                this.logger.LogDebug("Attempting to merge signatures for {0} and {1}.", this.PartialTransaction.GetHash(), incomingPartialTransaction.GetHash());

                this.PartialTransaction = SigningUtils.CombineSignatures(builder, this.PartialTransaction, new []{incomingPartialTransaction});

                if (oldTransaction.GetHash() == this.PartialTransaction.GetHash())
                {
                    // Signing didn't work if the hash is still the same
                    this.logger.LogDebug("Signing failed.");
                    return ConsolidationSignatureResult.Failed();
                }

                this.logger.LogDebug("Successfully signed transaction.");

                // NOTE: We don't need to reserve the transaction. The wallet will be at a standstill whilst this is happening.

                // If it is FullySigned, broadcast.
                if (this.walletManager.ValidateConsolidatingTransaction(this.PartialTransaction, true))
                {
                    this.logger.LogDebug("Consolidation transaction is fully signed. Broadcasting {0}", this.PartialTransaction.GetHash());
                    this.broadcasterManager.BroadcastTransactionAsync(this.PartialTransaction);
                    this.fullySigned = true;
                }

                this.logger.LogDebug("Consolidation transaction not fully signed yet.");

                return ConsolidationSignatureResult.Succeeded(this.PartialTransaction);
            }
        }

        /// <summary>
        /// Build a consolidating transaction.
        /// </summary>
        public Transaction BuildConsolidatingTransaction()
        {
            try
            {
                // TODO: Confirm that we can remove the ordering below.

                List<UnspentOutputReference> unspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(WithdrawalTransactionBuilder.MinConfirmations).ToList();

                // We shouldn't be consolidating transactions if we have less than 50 UTXOs to spend.
                if (unspentOutputs.Count < WithdrawalTransactionBuilder.MaxInputs)
                    return null;

                IEnumerable<UnspentOutputReference> orderedUnspentOutputs = DeterministicCoinOrdering.GetOrderedUnspentOutputs(unspentOutputs);
                IEnumerable<UnspentOutputReference> selectedInputs = orderedUnspentOutputs.Take(WithdrawalTransactionBuilder.MaxInputs);
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
                    Time = (uint?) selectedInputs.First().Transaction.CreationTime.ToUnixTimeSeconds() // TODO: Confirm this is the best answer
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
        public void ProcessBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            lock (this.lockObj)
            {
                if (!this.signingInProgress)
                    return;

                // If a consolidation transaction comes through, remove our progress.
                foreach (Transaction transaction in chainedHeaderBlock.Block.Transactions)
                {
                    if (transaction.Inputs.Count == WithdrawalTransactionBuilder.MaxInputs
                        && transaction.Outputs.Count == 1
                        && transaction.Outputs[0].ScriptPubKey == this.settings.MultiSigAddress.ScriptPubKey)
                    {
                        this.logger.LogDebug("Saw condensing transaction {0}, resetting InputConsolidator", transaction.GetHash());
                        this.PartialTransaction = null;
                        this.fullySigned = false;
                        this.signingInProgress = false;
                        return;
                    }
                }

                // Check that the consolidation transaction that we've built is still valid. In case of a reorg.
                if (!this.walletManager.ValidateConsolidatingTransaction(this.PartialTransaction))
                {
                    this.logger.LogDebug("Consolidation transaction {0} failed validation", this.PartialTransaction.GetHash());
                    this.PartialTransaction = null;
                    this.fullySigned = false;
                    this.signingInProgress = false;
                }
            }
        }
    }

    public class ConsolidationSignatureResult
    {
        /// <summary>
        /// Whether the transaction was successfully signed.
        /// </summary>
        public bool Signed { get; set; }

        /// <summary>
        /// The resulting transaction after signing.
        /// </summary>
        public Transaction TransactionResult { get; set; }

        public static ConsolidationSignatureResult Failed()
        {
            return new ConsolidationSignatureResult
            {
                Signed = false,
                TransactionResult = null
            };
        }

        public static ConsolidationSignatureResult Succeeded(Transaction result)
        {
            return new ConsolidationSignatureResult
            {
                Signed = true,
                TransactionResult = result
            };
        }
    }
}
