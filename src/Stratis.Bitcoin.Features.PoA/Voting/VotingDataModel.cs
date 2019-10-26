using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingDataModel
    {
        [JsonProperty("key")]
        public string Key { get; private set; }

        [JsonProperty("hash")]
        public string Hash { get; private set; }

        public VotingDataModel(VotingData votingData)
        {
            this.Key = votingData.Key.ToString();
            this.Hash = (new uint256(votingData.Data)).ToString();
        }

        [JsonConstructor]
        private VotingDataModel()
        {
        }
    }
}
