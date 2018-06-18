using System;
using NBitcoin;
using Newtonsoft.Json;
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

    internal class MonitorChainSession
    {
        public SessionStatus Status { get; set; }

        // Time when the session started.
        private readonly DateTime startTime;

        //Id of the session.
        public uint256 SessionId { get; }

        public Money Amount { get; set; }

        public string DestinationAddress { get; set; }

        public int BlockNumber { get; }

        // Boss table.
        public BossTable BossTable { get; }

        // My boss card. I only get to build and broadcast the transaction when my boss card is in play.
        public string BossCard { get; }

        public MonitorChainSession(DateTime startTime, uint256 transactionHash, Money amount, string destinationAddress,
            int blockNumber, Chain chain,  string[] federationPubKeys, string myPublicKey, int m, int n)
        {
            this.Status = SessionStatus.Created;
            this.startTime = startTime;
            this.SessionId = transactionHash;
            this.Amount = amount;
            this.DestinationAddress = destinationAddress;
            this.BlockNumber = blockNumber;

            // Build the boss table.
            this.BossTable = new BossTableBuilder().Build(this.SessionId, federationPubKeys);
            this.BossCard = BossTable.MakeBossTableEntry(transactionHash, myPublicKey).ToString();
        }

        public void Complete(uint256 counterChainTransactionId)
        {
            this.Status = SessionStatus.Completed;
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