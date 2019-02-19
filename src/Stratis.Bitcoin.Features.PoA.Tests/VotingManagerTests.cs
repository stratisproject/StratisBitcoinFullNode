using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class VotingManagerTests : PoATestsBase
    {
        private readonly VotingManager votingManager;
        private readonly Mock<IPollResultExecutor> resultExecutorMock;
        private readonly VotingDataEncoder encoder;

        private readonly List<VotingData> changesApplied;
        private readonly List<VotingData> changesReverted;

        private readonly ISignals signals;

        public VotingManagerTests()
        {
            string dir = TestBase.CreateTestDir(this);

            DataFolder folder = new DataFolder(dir);

            this.resultExecutorMock = new Mock<IPollResultExecutor>();
            this.encoder = new VotingDataEncoder(this.loggerFactory);
            this.changesApplied = new List<VotingData>();
            this.changesReverted = new List<VotingData>();

            this.resultExecutorMock.Setup(x => x.ApplyChange(It.IsAny<VotingData>())).Callback((VotingData data) => this.changesApplied.Add(data));
            this.resultExecutorMock.Setup(x => x.RevertChange(It.IsAny<VotingData>())).Callback((VotingData data) => this.changesReverted.Add(data));

            this.federationManager.SetPrivatePropertyValue(nameof(FederationManager.IsFederationMember), true);

            this.signals = new Signals.Signals();

            this.votingManager = new VotingManager(this.federationManager, this.loggerFactory, this.slotsManager, this.resultExecutorMock.Object,
                new NodeStats(new DateTimeProvider()), folder, new DBreezeSerializer(this.network), this.signals);
            this.votingManager.Initialize();
        }

        [Fact]
        public void CanScheduleAndRemoveVotes()
        {
            this.votingManager.ScheduleVote(new VotingData());

            Assert.Single(this.votingManager.GetScheduledVotes());

            this.votingManager.ScheduleVote(new VotingData());

            Assert.Equal(2, this.votingManager.GetAndCleanScheduledVotes().Count);

            Assert.Empty(this.votingManager.GetScheduledVotes());
        }

        [Fact]
        public void CanVote()
        {
            var votingData = new VotingData()
            {
                Key = VoteKey.AddFederationMember,
                Data = RandomUtils.GetBytes(20)
            };

            int votesRequired = (this.federationManager.GetFederationMembers().Count / 2) + 1;

            for (int i = 0; i < votesRequired; i++)
            {
                Assert.Empty(this.changesApplied);

                this.TriggerOnBlockConnected(this.CreateBlockWithVotingData(new List<VotingData>() { votingData }, i + 1));
            }

            Assert.Single(this.changesApplied);
        }

        private ChainedHeaderBlock CreateBlockWithVotingData(List<VotingData> data, int height)
        {
            var tx = new Transaction();

            var votingData = new List<byte>(VotingDataEncoder.VotingOutputPrefixBytes);
            votingData.AddRange(this.encoder.Encode(data));

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(votingData.ToArray()));

            tx.AddOutput(Money.COIN, votingOutputScript);

            Block block = new Block();
            block.Transactions.Add(tx);

            block.Header.Time = (uint)(height * (this.network.ConsensusOptions as PoAConsensusOptions).TargetSpacingSeconds);

            block.UpdateMerkleRoot();
            block.GetHash();

            return new ChainedHeaderBlock(block, new ChainedHeader(block.Header, block.GetHash(), 1));
        }

        private void TriggerOnBlockConnected(ChainedHeaderBlock block)
        {
            this.signals.OnBlockConnected.Notify(block);
        }

        private void TriggerOnBlockDisconnected(ChainedHeaderBlock block)
        {
            this.signals.OnBlockDisconnected.Notify(block);
        }
    }
}
