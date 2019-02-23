using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    /// <summary>Information about active poll.</summary>
    public class Poll : IBitcoinSerializable
    {
        public Poll()
        {
            this.PubKeysHexVotedInFavor = new List<string>();
        }

        /// <summary>
        /// <c>true</c> if poll still didn't get enough votes.
        /// <c>false</c> in case majority of fed members voted in favor and result of the poll was scheduled to be applied after max reorg blocks are mined.
        /// </summary>
        public bool IsPending => this.PollVotedInFavorBlockData == null;

        /// <summary><c>true</c> if poll wasn't executed yet; <c>false</c> otherwise.</summary>
        public bool IsExecuted => this.PollExecutedBlockData != null;

        public int Id;

        /// <summary>Data of a block where the poll got sufficient amount of votes. <c>null</c> if it number of votes is still insufficient.</summary>
        public HashHeightPair PollVotedInFavorBlockData;

        public VotingData VotingData;

        /// <summary>Data of a block where the poll was started.</summary>
        public HashHeightPair PollStartBlockData;

        /// <summary>Data of a block where the poll's changes were applied.</summary>
        public HashHeightPair PollExecutedBlockData;

        /// <summary>List of fed member's public keys that voted in favor.</summary>
        public List<string> PubKeysHexVotedInFavor;

        private List<PubKey> GetPubKeysVotedInFavor()
        {
            return this.PubKeysHexVotedInFavor?.Select(x => new PubKey(x)).ToList();
        }

        /// <inheritdoc />
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.Id);
            stream.ReadWrite(ref this.PollVotedInFavorBlockData);
            stream.ReadWrite(ref this.VotingData);
            stream.ReadWrite(ref this.PollStartBlockData);
            stream.ReadWrite(ref this.PollExecutedBlockData);

            if (stream.Serializing)
            {
                string[] arr = this.PubKeysHexVotedInFavor.ToArray();

                stream.ReadWrite(ref arr);
            }
            else
            {
                string[] arr = null;
                stream.ReadWrite(ref arr);

                this.PubKeysHexVotedInFavor = arr.ToList();
            }
        }

        public override string ToString()
        {
            return $"{nameof(this.Id)}:{this.Id}, {nameof(this.IsPending)}:{this.IsPending}, {nameof(this.IsExecuted)}:{this.IsExecuted}, " +
                   $"{nameof(this.PollStartBlockData)}:{this.PollStartBlockData?.ToString() ?? "null"}, " +
                   $"{nameof(this.PollVotedInFavorBlockData)}:{this.PollVotedInFavorBlockData?.ToString() ?? "null"}, " +
                   $"{nameof(this.PollExecutedBlockData)}:{this.PollExecutedBlockData?.ToString() ?? "null"}, " +
                   $"{nameof(this.PubKeysHexVotedInFavor)}:{string.Join(" ", this.PubKeysHexVotedInFavor)}";
        }
    }
}
