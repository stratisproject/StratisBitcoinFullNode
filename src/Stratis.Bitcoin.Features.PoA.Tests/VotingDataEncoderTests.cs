using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.PoA.Voting;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class VotingDataEncoderTests
    {
        private readonly VotingDataEncoder encoder;

        public VotingDataEncoderTests()
        {
            this.encoder = new VotingDataEncoder(new ExtendedLoggerFactory());
        }

        [Fact]
        public void GetVotingDataTest()
        {
            var tx = new Transaction();

            List<byte> votingData = new List<byte>(VotingDataEncoder.VotingOutputPrefixBytes);
            votingData.AddRange(RandomUtils.GetBytes(830));

            Script votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(votingData.ToArray()));

            tx.AddOutput(Money.COIN, votingOutputScript);
            tx.AddOutput(Money.COIN, Script.Empty);
            tx.AddOutput(Money.COIN, Script.Empty);

            byte[] extractedData = this.encoder.ExtractRawVotingData(tx);
            Assert.True(votingData.Skip(4).SequenceEqual(extractedData));

            // Add 2nd voting output.
            tx.AddOutput(Money.COIN, votingOutputScript);

            Assert.Throws<ConsensusErrorException>(() => this.encoder.ExtractRawVotingData(tx));
        }

        [Fact]
        public void GetVotingDataWorksWithDifferentDataLength()
        {
            Assert.NotNull(this.encoder.ExtractRawVotingData(this.GetTxWithVotingDataOfSize(50)));

            Assert.NotNull(this.encoder.ExtractRawVotingData(this.GetTxWithVotingDataOfSize(63535)));
        }

        [Fact]
        public void CanEncodeAndDecode()
        {
            var dataList = new List<VotingData>()
            {
                new VotingData()
                {
                    Key = VoteKey.AddFederationMember,
                    Data =  RandomUtils.GetBytes(25)
                },
                new VotingData()
                {
                    Key = VoteKey.KickFederationMember,
                    Data = RandomUtils.GetBytes(50)
                },
                new VotingData()
                {
                    Key = VoteKey.AddFederationMember,
                    Data = new byte[0]
                }
            };

            byte[] encodedBytes = this.encoder.Encode(dataList);

            List<VotingData> decoded = this.encoder.Decode(encodedBytes);

            byte[] encodedBytes2 = this.encoder.Encode(decoded);
            Assert.True(encodedBytes.SequenceEqual(encodedBytes2));

            for (int i = 0; i < dataList.Count; i++)
                Assert.Equal(dataList[i], decoded[i]);

            Assert.Equal(dataList.Count, decoded.Count);
        }

        [Fact]
        public void CanEncodeAndDecodeLargeAmountsOfData()
        {
            var dataList = new List<VotingData>();

            for (int i = 0; i < 2000; i++)
            {
                dataList.Add(new VotingData()
                {
                    Key = VoteKey.AddFederationMember,
                    Data = RandomUtils.GetBytes(25)
                });
            }

            byte[] encodedBytes = this.encoder.Encode(dataList);

            List<VotingData> decoded = this.encoder.Decode(encodedBytes);

            byte[] encodedBytes2 = this.encoder.Encode(decoded);
            Assert.True(encodedBytes.SequenceEqual(encodedBytes2));
        }

        [Fact]
        public void DecodeRandomData()
        {
            Assert.Throws<ConsensusErrorException>(() => this.encoder.Decode(new byte[] { 123, 55, 77, 14, 98, 12, 56, 0, 12 }));
        }

        private Transaction GetTxWithVotingDataOfSize(int size)
        {
            var tx = new Transaction();

            var votingData = new List<byte>(VotingDataEncoder.VotingOutputPrefixBytes);
            votingData.AddRange(RandomUtils.GetBytes(size));

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(votingData.ToArray()));

            tx.AddOutput(Money.COIN, votingOutputScript);

            return tx;
        }
    }
}
