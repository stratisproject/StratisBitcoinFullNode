namespace Stratis.FederatedSidechains.AdminDashboard.Entities
{
    public class PendingPoll
    {
        public int Id { get; set; }
        public bool IsPending { get; set; }
        public bool IsExecuted { get; set; }
        public string PollStartBlockData { get; set; }
        public string PollVotedInFavorBlockData { get; set; }
        public string PubKeysHexVotedInFavor { get; set; }
    }
}
