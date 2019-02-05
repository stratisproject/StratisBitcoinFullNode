using Moq;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class VotingManagerTests : PoATestsBase
    {
        private readonly VotingManager votingManager;
        private readonly Mock<IPollResultExecutor> resultExecutorMock;

        public VotingManagerTests()
        {
            string dir = TestBase.CreateTestDir(this);
            var keyValueRepo = new KeyValueRepository(dir, new DBreezeSerializer(this.network));
            this.resultExecutorMock = new Mock<IPollResultExecutor>();

            this.votingManager = new VotingManager(this.federationManager, this.loggerFactory, keyValueRepo, this.slotsManager, this.resultExecutorMock.Object);
        }

        [Fact]
        public void CanScheduleAndRemoveVotes()
        {
            this.federationManager.SetPrivatePropertyValue(nameof(FederationManager.IsFederationMember), true);
            this.votingManager.ScheduleVote(new VotingData());

            Assert.Single(this.votingManager.GetScheduledVotes());

            this.votingManager.ScheduleVote(new VotingData());

            Assert.Equal(2, this.votingManager.GetAndCleanScheduledVotes().Count);

            Assert.Empty(this.votingManager.GetScheduledVotes());
        }


        // TODO tests
        // add tests that will check reorg that adds or removes fed members
        // test that vote of a fed member that is no longer there is not active anymore
        // test case when we have 2 votes in a block and because 1 is executed before the other other no longer has enough votes
        // test that pending polls are always sorted by id
    }
}
