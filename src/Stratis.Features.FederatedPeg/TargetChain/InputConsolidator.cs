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

        private Transaction partialTransaction;

        private IFederationWalletTransactionHandler transactionHandler;

        public InputConsolidator(IFederationWalletTransactionHandler transactionHandler)
        {
            this.transactionHandler = transactionHandler;
        }

        public void StartConsolidation()
        {
            if (this.signingInProgress)
                return;

            this.signingInProgress = true;

            // Build condensing transaction in deterministic way
            Transaction transaction = this.BuildCondensingTransaction();

            // Store it somewhere on this component

            // Send it around to be signed

        }

        public void CombineSignatures()
        {
            if (!this.signingInProgress)
                return;

            // Check templates match

            // Sign

            // Send back
        }

        private Transaction BuildCondensingTransaction()
        {
            throw new NotImplementedException();
        }
    }
}
