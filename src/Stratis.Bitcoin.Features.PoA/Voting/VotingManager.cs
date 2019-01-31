using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingManager
    {
        private readonly FederationManager federationManager;

        private readonly ILogger logger;

        public VotingManager(FederationManager federationManager, ILoggerFactory loggerFactory)
        {
            this.federationManager = federationManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (!this.federationManager.IsFederationMember)
            {
                this.logger.LogTrace("(-)[NOT_FED_MEMBER]");
                return;
            }

            // TODO
        }

        // implement locks

        /// <summary>
        ///
        /// </summary>
        public void ScheduleVote(VotingData votingData)
        {

        }

        /// <summary>
        ///
        /// </summary>
        public List<VotingData> GetScheduledVotes()
        {

        }

        /// <summary>
        ///
        /// </summary>
        /// <remarks>Used by miner.</remarks>
        public List<VotingData> GetAndCleanScheduledVotes()
        {

        }






        /*
            It is subscribed to blocks updates and parses `voteOutput`'s of all blocks and keeps up to date view of existing votes.
            When majority votes for something this component applies the changes.

            Keep active votes as a data structure: `block in which vote was started, list of voters (pubKeys), voting type, voting data`

            When vote in favor comes check who voted against active fed members. only votes from active members count.

            // TODO it should handle reorgs and revert votes that were applied. Or introduce max reorg property.

            For particular keys there will be a requirement for N % of fed members
         */
    }
}
