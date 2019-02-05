using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.Voting;
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
                PollAppliedBlockHash = uint256.One,
                PollStartBlockHash = uint256.One,
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
            Assert.Equal(poll.PollAppliedBlockHash, deserializedPoll.PollAppliedBlockHash);
            Assert.Equal(poll.PollStartBlockHash, deserializedPoll.PollStartBlockHash);
            Assert.Equal(poll.PubKeysHexVotedInFavor.Count, deserializedPoll.PubKeysHexVotedInFavor.Count);

            for (int i = 0; i < poll.PubKeysHexVotedInFavor.Count; i++)
            {
                Assert.Equal(poll.PubKeysHexVotedInFavor[i], deserializedPoll.PubKeysHexVotedInFavor[i]);
            }
        }
    }
}
