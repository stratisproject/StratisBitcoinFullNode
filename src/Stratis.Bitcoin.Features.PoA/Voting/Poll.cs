using System.Collections.Generic;
using System.Linq;
using NBitcoin;

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
        /// <c>true</c> if poll's result wasn't applied.
        /// <c>false</c> in case majority of fed members voted in favor and result of the poll was applied.</summary>
        public bool IsPending => (this.PollAppliedBlockHash == null) || (this.PollAppliedBlockHash == uint256.Zero);

        public int Id;

        /// <summary>Hash of a block where the poll's result was applied. <c>null</c> if it wasn't applied.</summary>
        public uint256 PollAppliedBlockHash;

        public VotingData VotingData;

        /// <summary>Hash of a block where the poll was started.</summary>
        public uint256 PollStartBlockHash;

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
            stream.ReadWrite(ref this.PollAppliedBlockHash);
            stream.ReadWrite(ref this.VotingData);
            stream.ReadWrite(ref this.PollStartBlockHash);

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
            return $"{nameof(this.IsPending)}:{this.IsPending}, {nameof(this.Id)}:{this.Id}, {nameof(this.PollStartBlockHash)}:{this.PollStartBlockHash?.ToString() ?? "null"}, " +
                $"{nameof(this.PollAppliedBlockHash)}:{this.PollAppliedBlockHash?.ToString() ?? "null"}, {nameof(this.PubKeysHexVotedInFavor)}:{string.Join(" ", this.PubKeysHexVotedInFavor)}";
        }
    }
}
