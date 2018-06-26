using System;
using System.Collections;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

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

        // Time when the session started.
        private readonly DateTime startTime;

        public ICollection<CrossChainTransactionInfo> CrossChainTransactions { get; set; }

        
        public int BlockNumber { get; }

        // Boss table.
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

            // Build the boss table.
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

        /// <summary>
        /// Helper to generate a json respresentation of this structure for logging/debugging.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}