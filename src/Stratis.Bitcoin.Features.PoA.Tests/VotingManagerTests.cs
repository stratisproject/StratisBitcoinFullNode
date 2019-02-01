using Stratis.Bitcoin.Features.PoA.Voting;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class VotingManagerTests : PoATestsBase
    {
        private readonly VotingManager votingManager;

        public VotingManagerTests()
        {
            this.votingManager = new VotingManager(this.federationManager, this.loggerFactory);
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
    }
}
