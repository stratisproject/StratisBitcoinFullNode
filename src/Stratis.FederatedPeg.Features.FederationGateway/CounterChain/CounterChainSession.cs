using System.Collections.Generic;
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
        public bool HasReachedQuorum => 
            this.PartialTransactions.Count >= federationGatewaySettings.MultiSigM;
        public bool HaveISigned { get; set; } = false;
        public FederationGatewaySettings federationGatewaySettings { get; }

        private readonly ILogger logger;

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
            
            this.logger.LogDebug("Adding Partial to CounterChainSession.");
            
            // Insert the partial transaction in the session.
            this.PartialTransactions.Add(partialTransaction);
            
            // Output parts info.
            this.logger.LogDebug("New Partials");
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