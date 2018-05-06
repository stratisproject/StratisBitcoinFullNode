using System;
using NBitcoin;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    internal class BuildAndBroadcastSession
    {
        // Time when the session started.
        private DateTime startTime;

        //Id of the session.
        public uint256 SessionId { get; }

        public Money Amount { get; set; }

        public string DestinationAddress { get; set; }

        public enum SessionStatus { Created, Requesting, Requested, Completed }
        public SessionStatus Status { get; set; } = SessionStatus.Created;

        private uint256 completedCounterChainTransactionId;

        //boss table
        public BossTable BossTable { get; }

        // My boss card. I only get to build and broadcast the transaction when my boss card is in play.
        public string BossCard { get; }       

        public CrossChainTransactionInfo CrossChainTransactionInfo { get; set; }

        public BuildAndBroadcastSession(Chain chain, DateTime startTime, string memberFolderPath,
            uint256 transactionHash, string myPublicKey, string destinationAddress, Money amount)
        {
            this.startTime = startTime;
            this.SessionId = transactionHash;

            var memberFolderManager = new MemberFolderManager(memberFolderPath);
            var federation = memberFolderManager.LoadFederation(2, 3);
            this.BossTable = new BossTableBuilder().Build(this.SessionId, federation.GetPublicKeys(chain));
            this.BossCard = BossTable.MakeBossTableEntry(transactionHash, myPublicKey).ToString();
            this.Amount = amount;
            this.DestinationAddress = destinationAddress;
        }

        public void Complete(uint256 counterChainTransactionId)
        {
            this.completedCounterChainTransactionId = counterChainTransactionId;
            this.Status = SessionStatus.Completed;
        }

        private bool WeAreInFreeForAll(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now) == null;

        public bool AmITheBoss(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now) == this.BossCard;

        public string WhoHoldsTheBossCard(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now);
    }
}