using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingManager
    {
        /// <summary>Key for accessing list of pending polls from <see cref="IKeyValueRepository"/>.</summary>
        private const string PendingPollsDbKey = "pendingpollskey";

        /// <summary>Key for accessing list of pending polls from <see cref="IKeyValueRepository"/>.</summary>
        private const string FinishedPollsDbKey = "finishedpollskey";

        private readonly FederationManager federationManager;

        private readonly IKeyValueRepository keyValueRepo;

        private readonly ILogger logger;

        /// <summary>Protects access to <see cref="scheduledVotingData"/>, <see cref="pendingPolls"/>.</summary>
        private readonly object locker;

        /// <summary>Collection of voting data that should be included in a block when it's mined.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<VotingData> scheduledVotingData;

        /// <summary>Collection of pending polls.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<Poll> pendingPolls;

        public VotingManager(FederationManager federationManager, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo)
        {
            this.federationManager = federationManager;
            this.keyValueRepo = keyValueRepo;
            this.locker = new object();
            this.scheduledVotingData = new List<VotingData>();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (!this.federationManager.IsFederationMember)
            {
                this.logger.LogTrace("(-)[NOT_FED_MEMBER]");
                return;
            }

            this.pendingPolls = this.LoadPolls(PendingPollsDbKey);

            if (this.pendingPolls == null)
            {
                this.logger.LogDebug("No pending polls found in DB, initializing with empty collection.");

                this.pendingPolls = new List<Poll>();
                this.SavePolls(this.pendingPolls, PendingPollsDbKey);
            }
        }

        /// <summary>Schedules a vote for the next time when the block will be mined.</summary>
        public void ScheduleVote(VotingData votingData)
        {
            lock (this.locker)
            {
                this.scheduledVotingData.Add(votingData);
            }
        }

        /// <summary>Provides a copy of scheduled voting data.</summary>
        public List<VotingData> GetScheduledVotes()
        {
            lock (this.locker)
            {
                return new List<VotingData>(this.scheduledVotingData);
            }
        }

        /// <summary>Provides scheduled voting data and removes all items that were provided.</summary>
        /// <remarks>Used by miner.</remarks>
        public List<VotingData> GetAndCleanScheduledVotes()
        {
            lock (this.locker)
            {
                List<VotingData> votingData = this.scheduledVotingData;

                this.scheduledVotingData = new List<VotingData>();

                return votingData;
            }
        }

        /// <summary>Saves collection of polls to the database under provided key.</summary>
        private void SavePolls(List<Poll> polls, string key)
        {
            this.keyValueRepo.SaveValueJson(key, polls);
        }

        /// <summary>Loads collection of polls from the database using provided key.</summary>
        private List<Poll> LoadPolls(string key)
        {
            List<Poll> polls = this.keyValueRepo.LoadValueJson<List<Poll>>(key);
            return polls;
        }

        // TODO subscribe to signals
        private void onBlockConnected(ChainedHeaderBlock chBlock)
        {
            // parse voteOutputs
            // update pending polls

            // check if some polls are finished now (majority voted in favor), if true move them to finished polls
            // When vote in favor comes check who voted against active fed members. only votes from active members count.
        }

        // TODO subscribe to signals
        private void onBlockDisconnected(ChainedHeaderBlock chBlock)
        {

        }

        /*
            When vote in favor comes check who voted against active fed members. only votes from active members count.


            it should handle reorgs and revert votes that were applied. Or introduce max reorg property.
            For particular keys there will be a requirement for N % of fed members
         */
    }
}
