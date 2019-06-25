using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.FederatedSidechains.AdminDashboard.Entities
{
    public class PendingPoll
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("isPending")]
        public bool IsPending { get; set; }
        
        [JsonProperty("isExecuted")]
        public bool IsExecuted { get; set; }
        
        [JsonProperty("pollVotedInFavorBlockData")]
        public string PollVotedInFavorBlockData { get; set; }
        
        [JsonProperty("pollStartBlockData")]
        public string PollStartBlockData { get; set; }
        
        [JsonProperty("pollExecutedBlockData")]
        public string PollExecutedBlockData { get; set; }
        
        [JsonProperty("pubKeysHexVotedInFavor")]
        public JArray PubKeysHexVotedInFavor { get; set; }

        [JsonProperty("votingDataString")]
        public string VotingDataString { get; set; }
    }
}
