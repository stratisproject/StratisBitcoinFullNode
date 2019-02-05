using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly VotingDataEncoder votingDataEncoder;

        private readonly SlotsManager slotsManager;

        private readonly ILogger logger;

        /// <summary>Protects access to <see cref="scheduledVotingData"/>, <see cref="pendingPolls"/>.</summary>
        private readonly object locker;

        /// <summary>Collection of voting data that should be included in a block when it's mined.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<VotingData> scheduledVotingData;

        /// <summary>Collection of pending polls.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<Poll> pendingPolls;

        /// <summary>Collection of finished polls.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<Poll> finishedPolls;

        public VotingManager(FederationManager federationManager, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo, SlotsManager slotsManager)
        {
            this.federationManager = federationManager;
            this.keyValueRepo = keyValueRepo;
            this.locker = new object();
            this.votingDataEncoder = new VotingDataEncoder(loggerFactory);
            this.slotsManager = slotsManager;
            this.scheduledVotingData = new List<VotingData>();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.pendingPolls = this.LoadPolls(PendingPollsDbKey);

            if (this.pendingPolls == null)
            {
                this.logger.LogDebug("No pending polls found in DB, initializing with empty collection.");

                this.pendingPolls = new List<Poll>();
                this.SavePolls(this.pendingPolls, PendingPollsDbKey);
            }

            this.finishedPolls = this.LoadPolls(FinishedPollsDbKey);

            if (this.finishedPolls == null)
            {
                this.logger.LogDebug("No finished polls found in DB, initializing with empty collection.");

                this.finishedPolls = new List<Poll>();
                this.SavePolls(this.finishedPolls, FinishedPollsDbKey);
            }
        }

        /// <summary>Schedules a vote for the next time when the block will be mined.</summary>
        public void ScheduleVote(VotingData votingData)
        {
            if (!this.federationManager.IsFederationMember)
            {
                this.logger.LogTrace("(-)[NOT_FED_MEMBER]");
                throw new InvalidOperationException("Not a federation member!");
            }

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

        /// <summary>Provides a collection of polls that are currently active.</summary>
        public List<Poll> GetPendingPolls()
        {
            lock (this.locker)
            {
                return new List<Poll>(this.pendingPolls);
            }
        }

        /// <summary>Provides a collection of polls that are already finished and their results applied.</summary>
        public List<Poll> GetFinishedPolls()
        {
            lock (this.locker)
            {
                return new List<Poll>(this.finishedPolls);
            }
        }

        // TODO subscribe to signals
        // TODO more logs and log polls
        private void onBlockConnected(ChainedHeaderBlock chBlock)
        {
            byte[] rawVotingData = this.votingDataEncoder.ExtractRawVotingData(chBlock.Block.Transactions[0]);

            if (rawVotingData == null)
            {
                this.logger.LogTrace("(-)[NO_VOTING_DATA]");
                return;
            }

            // Pub key of a fed member that created voting data.
            string fedMemberKeyHex = this.slotsManager.GetPubKeyForTimestamp(chBlock.Block.Header.Time).ToHex();

            List<VotingData> votingDataList = this.votingDataEncoder.Decode(rawVotingData);

            lock (this.locker)
            {
                foreach (VotingData data in votingDataList)
                {
                    Poll existingPoll = this.pendingPolls.SingleOrDefault(x => x.VotingData == data);

                    if (existingPoll == null)
                    {
                        existingPoll = new Poll()
                        {
                            PollAppliedBlockHash = null,
                            PollStartBlockHash = chBlock.Block.GetHash(),
                            VotingData = data,
                            PubKeysHexVotedInFavor = new List<string>() { fedMemberKeyHex }
                        };

                        this.pendingPolls.Add(existingPoll);
                        this.logger.LogDebug("New poll was created.");
                    }
                    else if (!existingPoll.PubKeysHexVotedInFavor.Contains(fedMemberKeyHex))
                    {
                        existingPoll.PubKeysHexVotedInFavor.Add(fedMemberKeyHex);

                        this.logger.LogDebug("Voted on existing poll.");
                    }

                    List<string> fedMembersHex = this.federationManager.GetFederationMembers().Select(x => x.ToHex()).ToList();

                    // It is possible that there is a vote from a federation member that was deleted from the federation.
                    // Do not count votes from entities that are not active fed members.
                    int validVotesCount = existingPoll.PubKeysHexVotedInFavor.Count(x => fedMembersHex.Contains(x));

                    int requiredVotesCount = (fedMembersHex.Count / 2) + 1;

                    if (validVotesCount > requiredVotesCount)
                    {
                        // TODO apply changes, move active poll to finished polls, set finished block hash

                        // TODO to apply changes use voting data result executor that has methods like apply change and revert change
                    }
                }

                this.SavePolls(this.pendingPolls, PendingPollsDbKey);
                this.SavePolls(this.finishedPolls, FinishedPollsDbKey);
            }
        }

        // TODO subscribe to signals
        private void onBlockDisconnected(ChainedHeaderBlock chBlock)
        {
            // TODO it should handle reorgs and revert votes that were applied.
        }

        // TODO tests
        // add tests that will check reorg that adds or removes fed members
        // test that vote of a fed member that is no longer there is not active anymore
        // test case when we have 2 votes in a block and because 1 is executed before the other other no longer has enough votes
    }
}
