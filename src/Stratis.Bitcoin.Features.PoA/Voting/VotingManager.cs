﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingManager : IDisposable
    {
        private readonly FederationManager federationManager;

        private readonly VotingDataEncoder votingDataEncoder;

        private readonly SlotsManager slotsManager;

        private readonly IPollResultExecutor pollResultExecutor;

        private readonly ISignals signals;

        private readonly ILogger logger;

        /// <summary>Protects access to <see cref="scheduledVotingData"/>, <see cref="polls"/>, <see cref="pollsRepository"/>.</summary>
        private readonly object locker;

        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private readonly PollsRepository pollsRepository;

        /// <summary>In-memory collection of pending polls.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<Poll> polls;

        /// <summary>Collection of voting data that should be included in a block when it's mined.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private List<VotingData> scheduledVotingData;

        public VotingManager(FederationManager federationManager, ILoggerFactory loggerFactory, SlotsManager slotsManager, IPollResultExecutor pollResultExecutor,
            INodeStats nodeStats, DataFolder dataFolder, DBreezeSerializer dBreezeSerializer, ISignals signals)
        {
            this.federationManager = Guard.NotNull(federationManager, nameof(federationManager));
            this.slotsManager = Guard.NotNull(slotsManager, nameof(slotsManager));
            this.pollResultExecutor = Guard.NotNull(pollResultExecutor, nameof(pollResultExecutor));
            this.signals = Guard.NotNull(signals, nameof(signals));

            this.locker = new object();
            this.votingDataEncoder = new VotingDataEncoder(loggerFactory);
            this.scheduledVotingData = new List<VotingData>();
            this.pollsRepository = new PollsRepository(dataFolder, loggerFactory, dBreezeSerializer);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, 1200);
        }

        public void Initialize()
        {
            this.pollsRepository.Initialize();

            this.polls = this.pollsRepository.GetAllPolls();

            this.signals.OnBlockConnected.Attach(this.OnBlockConnected);
            this.signals.OnBlockDisconnected.Attach(this.OnBlockDisconnected);
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

        /// <summary>Provides a collection of polls that are currently active.</summary>
        public List<Poll> GetPendingPolls()
        {
            lock (this.locker)
            {
                return new List<Poll>(this.polls.Where(x => x.IsPending));
            }
        }

        /// <summary>Provides a collection of polls that are already finished and their results applied.</summary>
        public List<Poll> GetFinishedPolls()
        {
            lock (this.locker)
            {
                return new List<Poll>(this.polls.Where(x => !x.IsPending));
            }
        }

        // TODO more logs and log polls
        private void OnBlockConnected(ChainedHeaderBlock chBlock)
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
                    Poll existingPoll = this.polls.SingleOrDefault(x => x.VotingData == data && x.IsPending);

                    if (existingPoll == null)
                    {
                        existingPoll = new Poll()
                        {
                            Id = this.pollsRepository.GetHighestPollId() + 1,
                            PollAppliedBlockHash = null,
                            PollStartBlockHash = chBlock.Block.GetHash(),
                            VotingData = data,
                            PubKeysHexVotedInFavor = new List<string>() { fedMemberKeyHex }
                        };

                        this.polls.Add(existingPoll);
                        this.pollsRepository.AddPolls(existingPoll);

                        this.logger.LogDebug("New poll was created.");
                    }
                    else if (!existingPoll.PubKeysHexVotedInFavor.Contains(fedMemberKeyHex))
                    {
                        existingPoll.PubKeysHexVotedInFavor.Add(fedMemberKeyHex);
                        this.pollsRepository.UpdatePoll(existingPoll);

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
                        this.pollsRepository.UpdatePoll(existingPoll);
                    }
                }
            }
        }

        private void OnBlockDisconnected(ChainedHeaderBlock chBlock)
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
                    Poll targetPoll = this.polls.Single(x => x.VotingData == votingData);

                    if (targetPoll.PollAppliedBlockHash == chBlock.Block.GetHash())
                    {
                        this.pollResultExecutor.RevertChange(targetPoll.VotingData);

                        targetPoll.PollAppliedBlockHash = null;

                        this.pollsRepository.UpdatePoll(targetPoll);
                    }

                    // Pub key of a fed member that created voting data.
                    string fedMemberKeyHex = this.slotsManager.GetPubKeyForTimestamp(chBlock.Block.Header.Time).ToHex();

                    targetPoll.PubKeysHexVotedInFavor.Remove(fedMemberKeyHex);

                    if (targetPoll.PubKeysHexVotedInFavor.Count == 0)
                    {
                        this.polls.Remove(targetPoll);
                        this.pollsRepository.RemovePolls(targetPoll.Id);
                    }
                }
            }
        }

        private void AddComponentStats(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("======Voting Manager======");

            lock (this.locker)
            {
                log.AppendLine($"{this.polls.Count(x => x.IsPending)} polls are pending, {this.polls.Count(x => !x.IsPending)} polls are finished.");
                log.AppendLine($"{this.scheduledVotingData.Count} votes are scheduled to be added to the next block this node mines.");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.signals.OnBlockConnected.Detach(this.OnBlockConnected);
            this.signals.OnBlockDisconnected.Detach(this.OnBlockDisconnected);

            this.pollsRepository.Dispose();
        }
    }
}
