using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingManager
    {
        private readonly FederationManager federationManager;

        private readonly ILogger logger;

        /// <summary>Collection of voting data that should be included in a block when it's mined.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<VotingData> scheduledVotingData;

        /// <summary>Collection of pending polls.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<PendingPoll> pendingPolls;

        /// <summary>Protects access to <see cref="scheduledVotingData"/>, <see cref="pendingPolls"/>.</summary>
        private readonly object locker;

        public VotingManager(FederationManager federationManager, ILoggerFactory loggerFactory)
        {
            this.federationManager = federationManager;
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

            // TODO load pendingPolls from kv storage
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


        /*
            Component is subscribed to block updates and parses `voteOutput`'s of all blocks and keeps up to date view of pendingPolls.
            When majority votes for something this component applies the changes.

            When vote in favor comes check who voted against active fed members. only votes from active members count.


            it should handle reorgs and revert votes that were applied. Or introduce max reorg property.
            For particular keys there will be a requirement for N % of fed members
         */
    }
}
