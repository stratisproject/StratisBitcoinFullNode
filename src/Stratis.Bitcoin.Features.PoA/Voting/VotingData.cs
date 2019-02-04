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
        AddFederationMember = 1
    }

    /// <summary>Information about a single vote.</summary>
    public class VotingData : IBitcoinSerializable
    {
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

        public override bool Equals(object obj)
        {
            VotingData item = obj as VotingData;

            if (item == null)
            {
                return false;
            }

            return this == item;
        }

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
