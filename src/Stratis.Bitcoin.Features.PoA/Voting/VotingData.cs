using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    /// <summary>
    /// Type of vote that determines actions that will take place in case
    /// majority of federation members votes in favor.
    /// </summary>
    public enum VoteKey : ushort
    {
        /// <summary>Remove one of the federation members.</summary>
        KickFederationMember = 0,

        /// <summary>Add new federation member.</summary>
        AddFederationMember = 1,

        /// <summary>Add given hash to the database of hashes.</summary>
        WhitelistHash = 2,

        /// <summary>Remove given hash from the database of hashes.</summary>
        RemoveHash = 3
    }

    /// <summary>Information about a single vote.</summary>
    public class VotingData : IBitcoinSerializable
    {
        public VotingData()
        {
            this.key = 0;
            this.Data = new byte[0];
        }

        public VoteKey Key
        {
            get => (VoteKey)this.key;
            set => this.key = (ushort)value;
        }

        private ushort key;

        public byte[] Data;

        public static bool operator ==(VotingData a, VotingData b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            if (((object)a.Data == null) || ((object)b.Data == null))
                return false;

            return a.Key == b.Key && a.Data.SequenceEqual(b.Data);
        }

        public static bool operator !=(VotingData a, VotingData b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var item = obj as VotingData;

            if (item == null)
            {
                return false;
            }

            return this == item;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Data.GetHashCode() ^ this.key;
        }

        /// <inheritdoc />
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.key);

            if (stream.Serializing)
            {
                ushort dataSize = (ushort)this.Data.Length;

                stream.ReadWrite(ref dataSize);
                stream.ReadWrite(ref this.Data);
            }
            else
            {
                ushort dataSize = 0;
                stream.ReadWrite(ref dataSize);

                this.Data = new byte[dataSize];

                stream.ReadWrite(ref this.Data, 0, dataSize);
            }
        }
    }
}
