using System;
using System.Linq;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class PollsRepositoryTests
    {
        private readonly PollsRepository repository;

        public PollsRepositoryTests()
        {
            string dir = TestBase.CreateTestDir(this);

            this.repository = new PollsRepository(dir, new ExtendedLoggerFactory(), new DBreezeSerializer(new TestPoANetwork()));
            this.repository.Initialize();
        }

        [Fact]
        public void CantAddOrRemovePollsOutOfOrder()
        {
            Assert.Equal(-1, this.repository.GetHighestPollId());

            this.repository.AddPolls(new Poll() { Id = 0 });
            this.repository.AddPolls(new Poll() { Id = 1 });
            this.repository.AddPolls(new Poll() { Id = 2 });
            Assert.Throws<ArgumentException>(() => this.repository.AddPolls(new Poll() {Id = 5}));
            this.repository.AddPolls(new Poll() { Id = 3 });

            Assert.Equal(3, this.repository.GetHighestPollId());

            this.repository.RemovePolls(3);

            Assert.Throws<ArgumentException>(() => this.repository.RemovePolls(6));
            Assert.Throws<ArgumentException>(() => this.repository.RemovePolls(3));

            this.repository.RemovePolls(2);
            this.repository.RemovePolls(1);
            this.repository.RemovePolls(0);

            this.repository.Dispose();
        }

        [Fact]
        public void SavesHighestPollId()
        {
            this.repository.AddPolls(new Poll() { Id = 0 });
            this.repository.AddPolls(new Poll() { Id = 1 });
            this.repository.AddPolls(new Poll() { Id = 2 });

            this.repository.Initialize();

            Assert.Equal(2, this.repository.GetHighestPollId());
        }

        [Fact]
        public void CanLoadPolls()
        {
            this.repository.AddPolls(new Poll() { Id = 0 });
            this.repository.AddPolls(new Poll() { Id = 1 });
            this.repository.AddPolls(new Poll() { Id = 2 });

            Assert.True(this.repository.GetPolls(0, 1, 2).Count == 3);
            Assert.True(this.repository.GetAllPolls().Count == 3);

            Assert.Throws<ArgumentException>(() => this.repository.GetPolls(-1));
            Assert.Throws<ArgumentException>(() => this.repository.GetPolls(9));
        }

        [Fact]
        public void CanUpdatePolls()
        {
            var poll = new Poll() {Id = 0, VotingData = new VotingData() {Key = VoteKey.AddFederationMember}};
            this.repository.AddPolls(poll);

            poll.VotingData.Key = VoteKey.KickFederationMember;
            this.repository.UpdatePoll(poll);

            Assert.Equal(VoteKey.KickFederationMember, this.repository.GetPolls(poll.Id).First().VotingData.Key);
        }
    }
}
