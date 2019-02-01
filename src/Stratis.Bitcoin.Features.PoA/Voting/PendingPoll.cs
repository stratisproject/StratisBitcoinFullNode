using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    /// <summary>Information about active poll.</summary>
    public class PendingPoll
    {
        public VotingData votingData { get; set; }

        /// <summary>Hash of a block where the poll was started.</summary>
        public uint256 BlockHash { get; set; }

        /// <summary>List of fed member's public keys that voted in favor.</summary>
        public List<string> PubKeysHexVotedInFavor { get; set; }

        private List<PubKey> GetPubKeysVotedInFavor()
        {
            return this.PubKeysHexVotedInFavor?.Select(x => new PubKey(x)).ToList();
        }
    }
}
