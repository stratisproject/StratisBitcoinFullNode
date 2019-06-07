using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Payloads;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class InputConsolidator : IInputConsolidator
    {
        private readonly IFederatedPegBroadcaster federatedPegBroadcaster;
        private readonly IFederationWalletTransactionHandler transactionHandler;
        private readonly IFederationWalletManager walletManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IFederatedPegSettings settings;
        private readonly Network network;

        private bool signingInProgress;

        private bool fullySigned;

        /// <summary>
        /// Used to ensure only one operation is happening at a time.
        /// </summary>
        private readonly object lockObj = new object();

        /// <summary>
        ///  The signing-in-progress consolidation transaction.
        /// </summary>
        private Transaction partialTransaction;

        public InputConsolidator(IFederatedPegBroadcaster federatedPegBroadcaster,
            IFederationWalletTransactionHandler transactionHandler,
            IFederationWalletManager walletManager,
            IBroadcasterManager broadcasterManager,
            IFederatedPegSettings settings,
            Network network)
        {
            this.federatedPegBroadcaster = federatedPegBroadcaster;
            this.transactionHandler = transactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.network = network;
            this.settings = settings;
        }

        // TODO: Add logging.

        public void StartConsolidation()
        {
            // TODO: Should be in task
            lock (this.lockObj)
            {
                if (this.signingInProgress)
                    return;

                this.signingInProgress = true;

                // Build condensing transaction in deterministic way
                this.partialTransaction = this.BuildConsolidatingTransaction();

                // Send it around to be signed
                RequestPartialTransactionPayload payload = new RequestPartialTransactionPayload(RequestPartialTransactionPayload.ConsolidationDepositId).AddPartial(this.partialTransaction);
                this.federatedPegBroadcaster.BroadcastAsync(payload).GetAwaiter().GetResult(); // TODO: fix async
            }
        }

        public ConsolidationSignatureResult CombineSignatures(Transaction incomingPartialTransaction)
        {
            lock (this.lockObj)
            {
                // No need to sign in these cases.
                if (!this.signingInProgress || this.fullySigned)
                    return ConsolidationSignatureResult.Failed();

                // Attempt to merge signatures
                var builder = new TransactionBuilder(this.network);
                Transaction oldTransaction = this.partialTransaction;

                SigningUtils.CombineSignatures(builder, this.partialTransaction, new []{incomingPartialTransaction});

                if (oldTransaction.GetHash() == this.partialTransaction.GetHash())
                {
                    // Signing didn't work if the hash is still the same
                    return ConsolidationSignatureResult.Failed();
                }

                // NOTE: We don't need to reserve the transaction. The wallet will be at a standstill whilst this is happening.

                // If it is FullySigned, broadcast.
                if (this.walletManager.ValidateTransaction(this.partialTransaction, true))
                {
                    this.broadcasterManager.BroadcastTransactionAsync(this.partialTransaction);
                    this.fullySigned = true;
                }

                return ConsolidationSignatureResult.Succeeded(this.partialTransaction);
            }
        }

        public Transaction BuildConsolidatingTransaction()
        {
            List<UnspentOutputReference> unspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(WithdrawalTransactionBuilder.MinConfirmations).ToList();

            if (unspentOutputs.Count < WithdrawalTransactionBuilder.MaxInputs)
                throw new Exception("We shouldn't be consolidating transactions if we have less than 50 UTXOs to spend.");

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
                Recipients = new List<Recipient>
                {
                    new Recipient
                    {
                        ScriptPubKey = this.settings.MultiSigAddress.ScriptPubKey,
                        Amount = Money.Coins(0.001m) // The amount doesn't actually matter cos we're sending to ourselves.
                    }
                },
                TransactionFee = Money.Coins(0.0025m), // 50 inputs. This is roughly half the withdrawal fee. TODO: Consider this number
                SelectedInputs = selectedInputs.Select(u => u.ToOutPoint()).ToList(),
                AllowOtherInputs = false
            };


            Transaction transaction = this.transactionHandler.BuildTransaction(multiSigContext);

            //this.logger.LogDebug("Consolidating transaction = {0}", transaction.ToString(this.network, RawFormat.BlockExplorer));

            return transaction;
        }

        // TODO: Register Block signals
        public void ProcessBlock(Block block)
        {
            lock (this.lockObj)
            {
                if (!this.signingInProgress)
                    return;

                // If a consolidation transaction comes through, remove our progress.

                // Check that the consolidation transaction that we've built is still valid. In case of a reorg.

                throw new NotImplementedException();
            }
        }
    }

    public class ConsolidationSignatureResult
    {
        public bool Signed { get; set; }
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
