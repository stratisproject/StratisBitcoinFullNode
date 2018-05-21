using System;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public enum SessionStatus
    {
        Created,
        Requesting,
        Requested,
        RequestSending,
        Completed
    }

    internal class MonitorChainSession
    {
        public SessionStatus Status { get; set; } = SessionStatus.Created;

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

        uint256 counterChainTransactionId = uint256.Zero;

        public MonitorChainSession(DateTime startTime, uint256 transactionHash, Money amount, string destinationAddress,
            int blockNumber, Chain chain,  string memberFolderPath, string myPublicKey)
        {
            this.startTime = startTime;
            this.SessionId = transactionHash;
            this.Amount = amount;
            this.DestinationAddress = destinationAddress;
            this.BlockNumber = blockNumber;

            // Build the boss table.
            var memberFolderManager = new MemberFolderManager(memberFolderPath);
            var federation = memberFolderManager.LoadFederation(2, 3);
            this.BossTable = new BossTableBuilder().Build(this.SessionId, federation.GetPublicKeys(chain));
            this.BossCard = BossTable.MakeBossTableEntry(transactionHash, myPublicKey).ToString();
        }

        public void Complete(uint256 counterChainTransactionId)
        {
            this.counterChainTransactionId = counterChainTransactionId;
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