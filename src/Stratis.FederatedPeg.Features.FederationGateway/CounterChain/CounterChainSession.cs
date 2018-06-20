using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.CounterChain
{
    public class CounterChainSession
    {
        public List<Transaction> PartialTransactions { get; private set; }
        public uint256 SessionId { get; }
        public Money Amount { get; }
        public string Destination { get; }
        public bool HasReachedQuorum => this.PartialTransactions.Count >= federationGatewaySettings.MultiSigM;
        public bool HaveISigned { get; set; } = false;
        public FederationGatewaySettings federationGatewaySettings { get; }

        private readonly ILogger logger;

        // The transactionId of the completed transaction.
        public uint256 CounterChainTransactionId { get; internal set; } = uint256.Zero;

        public CounterChainSession(ILogger logger,
            FederationGatewaySettings federationGatewaySettings,
            uint256 sessionId,
            Money amount,
            string destination)
        {
            this.logger = logger;
            this.federationGatewaySettings = federationGatewaySettings;
            this.PartialTransactions = new List<Transaction>();
            this.SessionId = sessionId;
            this.Amount = amount;
            this.Destination = destination;
        }

        internal bool AddPartial(Transaction partialTransaction, string bossCard)
        {
            this.logger.LogTrace("()");
            if (partialTransaction == null)
            {
                this.logger.LogDebug("Skipped adding a null partial transaction");
                return false;
            }
            
            // Insert the partial transaction in the session if has not been added yet.
            if (!this.PartialTransactions.Any(pt => pt.GetHash() == partialTransaction.GetHash() && pt.Inputs.First().ScriptSig == partialTransaction.Inputs.First().ScriptSig))
            {
                this.logger.LogDebug("Adding Partial to CounterChainSession.");
                this.PartialTransactions.Add(partialTransaction);
            }
            else
            {
                this.logger.LogDebug("Partial already added to CounterChainSession.");
            }
            
            // Output parts info.
            this.logger.LogDebug("List of partials transactions");
            this.logger.LogDebug(" ---------");
            foreach (var p in PartialTransactions)
            {
                this.logger.LogDebug(p.ToHex());
            }
                
            // Have we reached Quorum?
            this.logger.LogDebug("---------");
            this.logger.LogDebug(string.Format("HasQuorum: {0}", this.HasReachedQuorum));
            this.logger.LogTrace("(-)");
            
            // End output. 
            return this.HasReachedQuorum;
        }
    }
}