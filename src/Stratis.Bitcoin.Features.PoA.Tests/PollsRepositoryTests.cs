using System;
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
    }
}
