using System;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    internal class JsonCrossChainTransactionAuditor : ICrossChainTransactionAuditor
    {
        // The filename varies depending on the chain we are monitoring (mainchain=deposits or sidechain=withdrawals).
        private string filename;

        // The storage used to store the cross chain transactions.
        private readonly FileStorage<JsonCrossChainTransactionStore> fileStorage;

        // The in memory cross chain transaction store.
        private JsonCrossChainTransactionStore crossChainTransactionStore;

        public JsonCrossChainTransactionAuditor(Network network, DataFolder dataFolder)
        {
            this.fileStorage = new FileStorage<JsonCrossChainTransactionStore>(dataFolder.WalletPath);

            // Initialize chain specifics.
            var chain = network.ToChain();
            this.filename = chain == Chain.Mainchain ? "deposit_transaction_store.json" : "withdrawal_transaction_store.json";
        }

        public void Initialize()
        {
            // Load the store.
            this.crossChainTransactionStore = this.LoadCrossChainTransactionStore();
        }

        // Load the store (creates if no store yet).
        private JsonCrossChainTransactionStore LoadCrossChainTransactionStore()
        {
            if (this.fileStorage.Exists(filename))
                return this.fileStorage.LoadByFileName(filename);

            // Create a new empty store.
            var transactionStore = new JsonCrossChainTransactionStore();
            this.fileStorage.SaveToFile(transactionStore, filename);
            return transactionStore;
        }

        private void SaveCrossChainTransactionStore()
        {
            if (this.crossChainTransactionStore != null) //if initialize was not called
                this.fileStorage.SaveToFile(this.crossChainTransactionStore, filename);
        }

        public void AddCrossChainTransactionInfo(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            this.crossChainTransactionStore.Add(crossChainTransactionInfo);
        }

        public void Load()
        {
            // Do nothing. Load is handled in Initialize
        }

        public void Commit()
        {
            this.SaveCrossChainTransactionStore();
        }

        public void AddCounterChainTransactionId(uint256 sessionId, uint256 counterChainTransactionId)
        {
            this.crossChainTransactionStore.AddCrossChainTransactionId(sessionId, counterChainTransactionId);
        }

        public void Dispose()
        {
            this.Commit();
        }
    }
}
