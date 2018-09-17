using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public enum SessionStatus
    {
        Created,
        Requesting,
        Requested,
        Completed
    }

    public class MonitorChainSession
    {
        public SessionStatus Status { get; set; }

        private readonly DateTime startTime;

        public ICollection<CrossChainTransactionInfo> CrossChainTransactions { get; set; }
        
        public int BlockNumber { get; }

        public BossTable BossTable { get; }

        // My boss card. I only get to build and broadcast the transaction when my boss card is in play.
        public string BossCard { get; }

        public uint256 CounterChainTransactionId { get; private set; } = uint256.Zero;

        public MonitorChainSession(int blockNumber, string[] federationPubKeys, string myPublicKey)
        {
            this.Status = SessionStatus.Created;
            this.startTime = DateTime.Now;
            this.CrossChainTransactions = new List<CrossChainTransactionInfo>();
            this.BlockNumber = blockNumber;

            this.BossTable = new BossTableBuilder().Build(blockNumber, federationPubKeys);
            this.BossCard = BossTable.MakeBossTableEntry(blockNumber, myPublicKey).ToString();
        }

        public void Complete(uint256 counterChainTransactionId)
        {
            this.Status = SessionStatus.Completed;
            this.CounterChainTransactionId = counterChainTransactionId;
        }

        private bool WeAreInFreeForAll(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now) == null;

        public bool AmITheBoss(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now) == this.BossCard;

        public string WhoHoldsTheBossCard(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now);

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}