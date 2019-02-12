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

        private readonly IPollResultExecutor pollResultExecutor;

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

        public VotingManager(FederationManager federationManager, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo,
            SlotsManager slotsManager, IPollResultExecutor pollResultExecutor)
        {
            this.federationManager = federationManager;
            this.keyValueRepo = keyValueRepo;
            this.slotsManager = slotsManager;
            this.pollResultExecutor = pollResultExecutor;

            this.locker = new object();
            this.votingDataEncoder = new VotingDataEncoder(loggerFactory);
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
        /// <exception cref="InvalidOperationException">Thrown in case caller is not a federation member.</exception>
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
            this.keyValueRepo.SaveValue(key, polls);
        }

        /// <summary>Loads collection of polls from the database using provided key.</summary>
        private List<Poll> LoadPolls(string key)
        {
            List<Poll> polls = this.keyValueRepo.LoadValue<List<Poll>>(key);
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
        // TODO make private when signaler PR is merged
        public void onBlockConnected(ChainedHeaderBlock chBlock)
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
                            Id = this.GetNextPollIdLocked(),
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
                    else
                    {
                        this.logger.LogDebug("Fed member '{0}' already voted for this poll. Ignoring his vote.", fedMemberKeyHex);
                    }

                    List<string> fedMembersHex = this.federationManager.GetFederationMembers().Select(x => x.ToHex()).ToList();

                    // It is possible that there is a vote from a federation member that was deleted from the federation.
                    // Do not count votes from entities that are not active fed members.
                    int validVotesCount = existingPoll.PubKeysHexVotedInFavor.Count(x => fedMembersHex.Contains(x));

                    int requiredVotesCount = (fedMembersHex.Count / 2) + 1;

                    if (validVotesCount >= requiredVotesCount)
                    {
                        this.pollResultExecutor.ApplyChange(data);

                        existingPoll.PollAppliedBlockHash = chBlock.Block.GetHash();
                        this.pendingPolls.Remove(existingPoll);
                        this.finishedPolls.Add(existingPoll);
                    }
                }

                this.SavePolls(this.pendingPolls, PendingPollsDbKey);
                this.SavePolls(this.finishedPolls, FinishedPollsDbKey);
            }
        }

        // TODO subscribe to signals
        // TODO make private when signaler PR is merged
        public void onBlockDisconnected(ChainedHeaderBlock chBlock)
        {
            byte[] rawVotingData = this.votingDataEncoder.ExtractRawVotingData(chBlock.Block.Transactions[0]);

            List<VotingData> votingDataList = this.votingDataEncoder.Decode(rawVotingData);
            votingDataList.Reverse();

            if (rawVotingData == null)
            {
                this.logger.LogTrace("(-)[NO_VOTING_DATA]");
                return;
            }

            lock (this.locker)
            {
                foreach (VotingData votingData in votingDataList)
                {
                    // Poll that was finished in the block being disconnected.
                    Poll pollToRevert = this.finishedPolls.SingleOrDefault(x => x.PollAppliedBlockHash == chBlock.Block.GetHash() && x.VotingData == votingData);

                    if (pollToRevert != null)
                    {
                        this.pollResultExecutor.RevertChange(pollToRevert.VotingData);

                        pollToRevert.PollAppliedBlockHash = null;

                        this.finishedPolls.Remove(pollToRevert);
                        this.pendingPolls.Add(pollToRevert);

                        this.pendingPolls = this.pendingPolls.OrderBy(x => x.Id).ToList();
                    }

                    // Pub key of a fed member that created voting data.
                    string fedMemberKeyHex = this.slotsManager.GetPubKeyForTimestamp(chBlock.Block.Header.Time).ToHex();

                    Poll targetPendingPoll = this.pendingPolls.Single(x => x.VotingData == votingData);

                    targetPendingPoll.PubKeysHexVotedInFavor.Remove(fedMemberKeyHex);

                    if (targetPendingPoll.PubKeysHexVotedInFavor.Count == 0)
                        this.pendingPolls.Remove(targetPendingPoll);
                }

                this.SavePolls(this.pendingPolls, PendingPollsDbKey);
                this.SavePolls(this.finishedPolls, FinishedPollsDbKey);
            }
        }

        /// <summary>Provides id for the next poll that is to be created.</summary>
        /// <remarks>Should be locked by <see cref="locker"/>.</remarks>
        private int GetNextPollIdLocked()
        {
            int largestId = -1;

            foreach (Poll finishedPoll in this.finishedPolls)
            {
                if (finishedPoll.Id > largestId)
                    largestId = finishedPoll.Id;
            }

            foreach (Poll pendingPoll in this.pendingPolls)
            {
                if (pendingPoll.Id > largestId)
                    largestId = pendingPoll.Id;
            }

            return largestId + 1;
        }
    }
}
