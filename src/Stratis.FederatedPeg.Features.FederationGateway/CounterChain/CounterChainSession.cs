using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway;

public class CounterChainSession
    {
        private Transaction[] partialTransactions;
        public uint256 SessionId { get; }
        public Money Amount { get; }
        public string Destination { get; }
        // todo: we can remove this if we just use a list for the partials
        private BossTable bossTable;
        public bool HasReachedQuorum { get; private set; }
        public Transaction[] PartialTransactions => this.partialTransactions;
        private ILogger logger;
        public bool HaveISigned { get; set; } = false;
        public CounterChainSession(ILogger logger, int federationSize, uint256 sessionId, string[] addresses, Money amount, string destination)
        {
            this.logger = logger;
            this.partialTransactions = new Transaction[federationSize];
            this.SessionId = sessionId;
            this.Amount = amount;
            this.Destination = destination;
            this.bossTable = new BossTableBuilder().Build(sessionId, addresses);
        }
        internal bool AddPartial(string memberName, Transaction partialTransaction, string bossCard)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{memberName} Adding Partial to MonitorChainSession.");
            // Insert the partial transaction in the session.
            int positionInTable = 0;
            for (; positionInTable < 3; ++positionInTable )
                if (bossCard == bossTable.BossTableEntries[positionInTable])
                    break;
            this.partialTransactions[positionInTable] = partialTransaction;
            // Have we reached Quorum?
            this.HasReachedQuorum = this.CountPartials() >= 2;
            // Output parts info.
            this.logger.LogInformation($"{memberName} New Partials");
            this.logger.LogInformation($"{memberName} ---------");
            foreach (var p in partialTransactions)
            {
                if (p == null)
                    this.logger.LogInformation($"{memberName} null");
                else
                    this.logger.LogInformation($"{memberName} {p?.ToHex()}");
            }
            this.logger.LogInformation($"{memberName} ---------");
            this.logger.LogInformation($"{memberName} HasQuorum: {this.HasReachedQuorum}");
            this.logger.LogInformation("(-)");
            // End output. 
            return this.HasReachedQuorum;
        }
        private int CountPartials()
        {
            int positionInTable = 0;
            int count = 0;
            for (; positionInTable < 3; ++positionInTable)
                if (partialTransactions[positionInTable] != null)
                    ++count;
            return count;
        }
    }