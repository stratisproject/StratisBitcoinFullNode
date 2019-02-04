using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class VotingManagerTests : PoATestsBase
    {
        private readonly VotingManager votingManager;

        public VotingManagerTests()
        {
            string dir = TestBase.CreateTestDir(this);
            var keyValueRepo = new KeyValueRepository(dir, new DBreezeSerializer(this.network));

            this.votingManager = new VotingManager(this.federationManager, this.loggerFactory, keyValueRepo, this.slotsManager);
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
    }
}
