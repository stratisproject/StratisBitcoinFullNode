using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingManager : IDisposable
    {
        private readonly IFederationManager federationManager;

        private readonly VotingDataEncoder votingDataEncoder;

        private readonly ISlotsManager slotsManager;

        private readonly IPollResultExecutor pollResultExecutor;

        private readonly ISignals signals;

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        private readonly IFinalizedBlockInfoRepository finalizedBlockInfo;

        /// <summary>Protects access to <see cref="scheduledVotingData"/>, <see cref="polls"/>, <see cref="pollsRepository"/>.</summary>
        private readonly object locker;

        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private readonly PollsRepository pollsRepository;

        /// <summary>In-memory collection of pending polls.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<Poll> polls;

        private SubscriptionToken blockConnectedSubscription;
        private SubscriptionToken blockDisconnectedSubscription;

        /// <summary>Collection of voting data that should be included in a block when it's mined.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<VotingData> scheduledVotingData;

        private bool isInitialized;

        public VotingManager(IFederationManager federationManager, ILoggerFactory loggerFactory, ISlotsManager slotsManager, IPollResultExecutor pollResultExecutor,
            INodeStats nodeStats, DataFolder dataFolder, DBreezeSerializer dBreezeSerializer, ISignals signals, IFinalizedBlockInfoRepository finalizedBlockInfo)
        {
            this.federationManager = Guard.NotNull(federationManager, nameof(federationManager));
            this.slotsManager = Guard.NotNull(slotsManager, nameof(slotsManager));
            this.pollResultExecutor = Guard.NotNull(pollResultExecutor, nameof(pollResultExecutor));
            this.signals = Guard.NotNull(signals, nameof(signals));
            this.nodeStats = Guard.NotNull(nodeStats, nameof(nodeStats));
            this.finalizedBlockInfo = Guard.NotNull(finalizedBlockInfo, nameof(finalizedBlockInfo));

            this.locker = new object();
            this.votingDataEncoder = new VotingDataEncoder(loggerFactory);
            this.scheduledVotingData = new List<VotingData>();
            this.pollsRepository = new PollsRepository(dataFolder, loggerFactory, dBreezeSerializer);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.isInitialized = false;
        }

        public void Initialize()
        {
            this.pollsRepository.Initialize();

            this.polls = this.pollsRepository.GetAllPolls();

            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(this.OnBlockDisconnected);

            this.nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name, 1200);

            this.isInitialized = true;
            this.logger.LogDebug("VotingManager initialized.");
        }

        /// <summary>Schedules a vote for the next time when the block will be mined.</summary>
        /// <exception cref="InvalidOperationException">Thrown in case caller is not a federation member.</exception>
        public void ScheduleVote(VotingData votingData)
        {
            this.EnsureInitialized();

            if (!this.federationManager.IsFederationMember)
            {
                this.logger.LogTrace("(-)[NOT_FED_MEMBER]");
                throw new InvalidOperationException("Not a federation member!");
            }

            lock (this.locker)
            {
                this.scheduledVotingData.Add(votingData);

                this.CleanFinishedPollsLocked();
            }

            this.logger.LogDebug("Vote was scheduled with key: {0}.", votingData.Key);
        }

        /// <summary>Provides a copy of scheduled voting data.</summary>
        public List<VotingData> GetScheduledVotes()
        {
            this.EnsureInitialized();

            lock (this.locker)
            {
                this.CleanFinishedPollsLocked();

                return new List<VotingData>(this.scheduledVotingData);
            }
        }

        /// <summary>Provides scheduled voting data and removes all items that were provided.</summary>
        /// <remarks>Used by miner.</remarks>
        public List<VotingData> GetAndCleanScheduledVotes()
        {
            this.EnsureInitialized();

            lock (this.locker)
            {
                this.CleanFinishedPollsLocked();

                List<VotingData> votingData = this.scheduledVotingData;

                this.scheduledVotingData = new List<VotingData>();

                if (votingData.Count > 0)
                    this.logger.LogDebug("{0} scheduled votes were taken.", votingData.Count);

                return votingData;
            }
        }

        /// <summary>Checks pending polls against finished polls and removes pending polls that will make no difference and basically are redundant.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private void CleanFinishedPollsLocked()
        {
            // We take polls that are not pending (collected enough votes in favor) but not executed yet (maxReorg blocks
            // didn't pass since the vote that made the poll pass). We can't just take not pending polls because of the
            // following scenario: federation adds a hash or fed member or does any other revertable action, then reverts
            // the action (removes the hash) and then reapplies it again. To allow for this scenario we have to exclude
            // executed polls here.
            List<Poll> finishedPolls = this.polls.Where(x => !x.IsPending && !x.IsExecuted).ToList();

            for (int i = this.scheduledVotingData.Count - 1; i >= 0; i--)
            {
                VotingData currentScheduledData = this.scheduledVotingData[i];

                // Remove scheduled voting data that can be found in finished polls that were not yet executed.
                if (finishedPolls.Any(x => x.VotingData == currentScheduledData))
                    this.scheduledVotingData.RemoveAt(i);
            }
        }

        /// <summary>Provides a collection of polls that are currently active.</summary>
        public List<Poll> GetPendingPolls()
        {
            this.EnsureInitialized();

            lock (this.locker)
            {
                return new List<Poll>(this.polls.Where(x => x.IsPending));
            }
        }

        /// <summary>Provides a collection of polls that are already finished and their results applied.</summary>
        public List<Poll> GetFinishedPolls()
        {
            this.EnsureInitialized();

            lock (this.locker)
            {
                return new List<Poll>(this.polls.Where(x => !x.IsPending));
            }
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            ChainedHeaderBlock chBlock = blockConnected.ConnectedBlock;
            uint256 newFinalizedHash = this.finalizedBlockInfo.GetFinalizedBlockInfo().Hash;

            lock (this.locker)
            {
                foreach (Poll poll in this.polls.Where(x => !x.IsPending && x.PollVotedInFavorBlockData.Hash == newFinalizedHash).ToList())
                {
                    this.logger.LogDebug("Applying poll '{0}'.", poll);
                    this.pollResultExecutor.ApplyChange(poll.VotingData);

                    poll.PollExecutedBlockData = new HashHeightPair(chBlock.ChainedHeader);
                    this.pollsRepository.UpdatePoll(poll);
                }
            }

            byte[] rawVotingData = this.votingDataEncoder.ExtractRawVotingData(chBlock.Block.Transactions[0]);

            if (rawVotingData == null)
            {
                this.logger.LogTrace("(-)[NO_VOTING_DATA]");
                return;
            }

            // Pub key of a fed member that created voting data.
            string fedMemberKeyHex = this.slotsManager.GetFederationMemberForTimestamp(chBlock.Block.Header.Time).PubKey.ToHex();

            List<VotingData> votingDataList = this.votingDataEncoder.Decode(rawVotingData);

            this.logger.LogDebug("Applying {0} voting data items included in a block by '{1}'.", votingDataList.Count, fedMemberKeyHex);

            lock (this.locker)
            {
                foreach (VotingData data in votingDataList)
                {
                    Poll poll = this.polls.SingleOrDefault(x => x.VotingData == data && x.IsPending);

                    if (poll == null)
                    {
                        poll = new Poll()
                        {
                            Id = this.pollsRepository.GetHighestPollId() + 1,
                            PollVotedInFavorBlockData = null,
                            PollExecutedBlockData = null,
                            PollStartBlockData = new HashHeightPair(chBlock.ChainedHeader),
                            VotingData = data,
                            PubKeysHexVotedInFavor = new List<string>() { fedMemberKeyHex }
                        };

                        this.polls.Add(poll);
                        this.pollsRepository.AddPolls(poll);

                        this.logger.LogDebug("New poll was created: '{0}'.", poll);
                    }
                    else if (!poll.PubKeysHexVotedInFavor.Contains(fedMemberKeyHex))
                    {
                        poll.PubKeysHexVotedInFavor.Add(fedMemberKeyHex);
                        this.pollsRepository.UpdatePoll(poll);

                        this.logger.LogDebug("Voted on existing poll: '{0}'.", poll);
                    }
                    else
                    {
                        this.logger.LogDebug("Fed member '{0}' already voted for this poll. Ignoring his vote. Poll: '{1}'.", fedMemberKeyHex, poll);
                    }

                    List<string> fedMembersHex = this.federationManager.GetFederationMembers().Select(x => x.PubKey.ToHex()).ToList();

                    // It is possible that there is a vote from a federation member that was deleted from the federation.
                    // Do not count votes from entities that are not active fed members.
                    int validVotesCount = poll.PubKeysHexVotedInFavor.Count(x => fedMembersHex.Contains(x));

                    int requiredVotesCount = (fedMembersHex.Count / 2) + 1;

                    this.logger.LogDebug("Fed members count: {0}, valid votes count: {1}, required votes count: {2}.", fedMembersHex.Count, validVotesCount, requiredVotesCount);

                    if (validVotesCount < requiredVotesCount)
                        continue;

                    poll.PollVotedInFavorBlockData = new HashHeightPair(chBlock.ChainedHeader);
                    this.pollsRepository.UpdatePoll(poll);
                }
            }
        }

        private void OnBlockDisconnected(BlockDisconnected blockDisconnected)
        {
            ChainedHeaderBlock chBlock = blockDisconnected.DisconnectedBlock;

            lock (this.locker)
            {
                foreach (Poll poll in this.polls.Where(x => !x.IsPending && x.PollExecutedBlockData?.Hash == chBlock.ChainedHeader.HashBlock).ToList())
                {
                    this.logger.LogDebug("Reverting poll execution '{0}'.", poll);
                    this.pollResultExecutor.RevertChange(poll.VotingData);

                    poll.PollExecutedBlockData = null;
                    this.pollsRepository.UpdatePoll(poll);
                }
            }

            byte[] rawVotingData = this.votingDataEncoder.ExtractRawVotingData(chBlock.Block.Transactions[0]);

            if (rawVotingData == null)
            {
                this.logger.LogTrace("(-)[NO_VOTING_DATA]");
                return;
            }

            List<VotingData> votingDataList = this.votingDataEncoder.Decode(rawVotingData);
            votingDataList.Reverse();

            lock (this.locker)
            {
                foreach (VotingData votingData in votingDataList)
                {
                    // If the poll is pending, that's the one we want. There should be maximum 1 of these.
                    Poll targetPoll = this.polls.SingleOrDefault(x => x.VotingData == votingData && x.IsPending);

                    // Otherwise, get the most recent poll. There could currently be unlimited of these, though they're harmless.
                    if (targetPoll == null)
                    {
                        targetPoll = this.polls.Last(x => x.VotingData == votingData);
                    }

                    this.logger.LogDebug("Reverting poll voting in favor: '{0}'.", targetPoll);

                    if (targetPoll.PollVotedInFavorBlockData == new HashHeightPair(chBlock.ChainedHeader))
                    {
                        targetPoll.PollVotedInFavorBlockData = null;

                        this.pollsRepository.UpdatePoll(targetPoll);
                    }

                    // Pub key of a fed member that created voting data.
                    string fedMemberKeyHex = this.slotsManager.GetFederationMemberForTimestamp(chBlock.Block.Header.Time).PubKey.ToHex();

                    targetPoll.PubKeysHexVotedInFavor.Remove(fedMemberKeyHex);

                    if (targetPoll.PubKeysHexVotedInFavor.Count == 0)
                    {
                        this.polls.Remove(targetPoll);
                        this.pollsRepository.RemovePolls(targetPoll.Id);

                        this.logger.LogDebug("Poll with Id {0} was removed.", targetPoll.Id);
                    }
                }
            }
        }

        [NoTrace]
        private void AddComponentStats(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("======Voting Manager======");

            lock (this.locker)
            {
                log.AppendLine($"{this.polls.Count(x => x.IsPending)} polls are pending, {this.polls.Count(x => !x.IsPending)} polls are finished, {this.polls.Count(x => x.IsExecuted)} polls are executed.");
                log.AppendLine($"{this.scheduledVotingData.Count} votes are scheduled to be added to the next block this node mines.");
            }
        }

        [NoTrace]
        private void EnsureInitialized()
        {
            if (!this.isInitialized)
            {
                throw new Exception("VotingManager is not initialized. Check that voting is enabled in PoAConsensusOptions.");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.signals.Unsubscribe(this.blockConnectedSubscription);
            this.signals.Unsubscribe(this.blockDisconnectedSubscription);

            this.pollsRepository.Dispose();
        }
    }
}
