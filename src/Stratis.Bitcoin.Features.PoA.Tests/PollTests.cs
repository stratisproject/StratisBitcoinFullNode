using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class PollTests
    {
        [Fact]
        public void CanSerializeAndDeserialize()
        {
            Poll poll = new Poll()
            {
                Id = 5,
                VotingData = new VotingData()
                {
                    Data = RandomUtils.GetBytes(50),
                    Key = VoteKey.AddFederationMember
                },
                PollVotedInFavorBlockData = new HashHeightPair(uint256.One, 1),
                PollStartBlockData = new HashHeightPair(uint256.One, 1),
                PubKeysHexVotedInFavor = new List<string>()
                {
                    "qwe",
                    "rty"
                }
            };

            byte[] serializedBytes;

            using (var memoryStream = new MemoryStream())
            {
                var serializeStream = new BitcoinStream(memoryStream, true);

                serializeStream.ReadWrite(ref poll);

                serializedBytes = memoryStream.ToArray();
            }

            var deserializedPoll = new Poll();

            using (var memoryStream = new MemoryStream(serializedBytes))
            {
                var deserializeStream = new BitcoinStream(memoryStream, false);

                deserializeStream.ReadWrite(ref deserializedPoll);
            }

            Assert.Equal(poll.Id, deserializedPoll.Id);
            Assert.Equal(poll.VotingData, deserializedPoll.VotingData);
            Assert.Equal(poll.PollVotedInFavorBlockData, deserializedPoll.PollVotedInFavorBlockData);
            Assert.Equal(poll.PollStartBlockData, deserializedPoll.PollStartBlockData);
            Assert.Equal(poll.PubKeysHexVotedInFavor.Count, deserializedPoll.PubKeysHexVotedInFavor.Count);

            for (int i = 0; i < poll.PubKeysHexVotedInFavor.Count; i++)
            {
                Assert.Equal(poll.PubKeysHexVotedInFavor[i], deserializedPoll.PubKeysHexVotedInFavor[i]);
            }
        }
    }
}
