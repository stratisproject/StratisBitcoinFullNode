using System;
using System.Collections.Generic;
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
        private readonly Network network;

        private bool signingInProgress;

        private bool fullySigned;

        /// <summary>
        /// Used to ensure only one operation is happening at a time.
        /// </summary>
        private object lockObj = new object();

        /// <summary>
        ///  TODO store in-progress signing here.
        /// </summary>
        private Transaction partialTransaction;

        public InputConsolidator(IFederatedPegBroadcaster federatedPegBroadcaster,
            IFederationWalletTransactionHandler transactionHandler,
            IFederationWalletManager walletManager,
            IBroadcasterManager broadcasterManager,
            Network network)
        {
            this.federatedPegBroadcaster = federatedPegBroadcaster;
            this.transactionHandler = transactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.network = network;
        }

        // TODO: Watch the wallet somewhere to check that the transaction has come through, so this component can be reset - we don't need it anymore.

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
                this.partialTransaction = this.BuildCondensingTransaction();

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
                }

                return ConsolidationSignatureResult.Succeeded(this.partialTransaction);
            }
        }

        public Transaction BuildCondensingTransaction()
        {
            throw new NotImplementedException();
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
