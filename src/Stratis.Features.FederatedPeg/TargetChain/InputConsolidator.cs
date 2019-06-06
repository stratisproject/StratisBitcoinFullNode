using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class InputConsolidator : IInputConsolidator
    {
        private bool signingInProgress;

        /// <summary>
        ///  TODO store in-progress signing here.
        /// </summary>
        private Transaction partialTransaction;

        private IFederationWalletTransactionHandler transactionHandler;

        public InputConsolidator(IFederationWalletTransactionHandler transactionHandler)
        {
            this.transactionHandler = transactionHandler;
        }

        // TODO: Watch the wallet somewhere to check that the transaction has come through, so this component can be reset - we don't need it anymore.

        public void StartConsolidation()
        {
            if (this.signingInProgress)
                return;

            this.signingInProgress = true;

            // Build condensing transaction in deterministic way
            Transaction transaction = this.BuildCondensingTransaction();

            // Store it somewhere on this component

            // Send it around to be signed - See code in PartialTransactionsRequester

        }


        public void CombineSignatures()
        {
            // Call into here from PartialTransactionsBehaviour. Have a special template, maybe null deposit id, that directs here.

            // TODO: Lock?

            if (!this.signingInProgress)
                return;

            // Check templates match - see code in CrossChainTransferStore

            // Sign

            // Store again

            // Send back
        }

        private Transaction BuildCondensingTransaction()
        {
            throw new NotImplementedException();
        }
    }
}
