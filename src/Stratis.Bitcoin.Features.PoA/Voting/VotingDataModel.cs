using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingDataModel
    {
        public string key { get; private set; }
        public string hash { get; private set; }

        public VotingDataModel(VotingData votingData)
        {
            this.key = votingData.Key.ToString();
            this.hash = (new uint256(votingData.Data)).ToString();
        }
    }
}
