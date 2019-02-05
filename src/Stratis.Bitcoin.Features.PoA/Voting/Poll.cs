using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    /// <summary>Information about active poll.</summary>
    public class Poll
    {
        /// <summary>
        /// <c>true</c> if poll's result wasn't applied.
        /// <c>false</c> in case majority of fed members voted in favor and result of the poll was applied.</summary>
        public bool IsActive => this.PollAppliedBlockHash == null;

        /// <summary>Hash of a block where the poll's result was applied. <c>null</c> if it wasn't applied.</summary>
        public uint256 PollAppliedBlockHash { get; set; }

        public VotingData VotingData { get; set; }

        /// <summary>Hash of a block where the poll was started.</summary>
        public uint256 PollStartBlockHash { get; set; }

        /// <summary>List of fed member's public keys that voted in favor.</summary>
        public List<string> PubKeysHexVotedInFavor { get; set; }

        private List<PubKey> GetPubKeysVotedInFavor()
        {
            return this.PubKeysHexVotedInFavor?.Select(x => new PubKey(x)).ToList();
        }
    }
}
