﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusException : Exception
    {
        public ConsensusException() : base()
        {
        }

        public ConsensusException(string messsage) : base(messsage)
        {
        }
    }

    public class MaxReorgViolationException : ConsensusException
    {
        public MaxReorgViolationException() : base()
        {
        }
    }

    public class ConnectHeaderException : ConsensusException
    {
        public ConnectHeaderException() : base()
        {
        }
    }

    public class InvalidHeaderException : ConsensusException
    {
        public InvalidHeaderException() : base()
        {
        }
    }

    public interface IChainedHeaderValidator
    {
        void Validate(ChainedHeader chainedHeader);
    }

    public class ConnectedHeaders
    {
        public ChainedHeader DownloadFrom { get; set; }

        public ChainedHeader DownloadTo { get; set; }

        public ChainedHeader Consumed { get; set; }

        public override string ToString()
        {
            return $"DownloadFrom={this.DownloadFrom},DownloadTo={this.DownloadTo},Consumed={this.Consumed}";
        }
    }

    /// <summary>
    /// A tree structure of <see cref="ChainedHeader "/> elements.
    /// </summary>
    /// <remarks>
    /// A blockchain can have multiple versions of the chain, with only one being the longest chain (or the chain with most proof).
    /// While this is not ideal it can happen naturally or maliciously, to be able to find the best chain we have to keep track of all chains we discover.
    /// Only one chain will represent the tip of the blockchain. 
    /// </remarks>
    public sealed class ChainedHeaderTree
    {
        private readonly Network network;
        private readonly IChainedHeaderValidator chainedHeaderValidator;
        private readonly ILogger logger;
        private readonly ICheckpoints checkpoints;
        private readonly IChainState chainState;
        private readonly ConsensusSettings consensusSettings;

        private readonly Dictionary<uint256, HashSet<int>> peerTipsByHash;
        private readonly Dictionary<uint256, ChainedHeader> chainedHeadersByHash;

        internal Dictionary<uint256, ChainedHeader> GetChainedHeadersByHash => this.chainedHeadersByHash;
        internal Dictionary<uint256, HashSet<int>> GetPeerTipsByHash => this.peerTipsByHash;

        private readonly object lockObject;

        public ChainedHeaderTree(
            Network network, 
            ILoggerFactory loggerFactory, 
            IChainedHeaderValidator chainedHeaderValidator, 
            ICheckpoints checkpoints, 
            IChainState chainState, 
            ConsensusSettings consensusSettings)
        {
            this.network = network;
            this.chainedHeaderValidator = chainedHeaderValidator;
            this.checkpoints = checkpoints;
            this.chainState = chainState;
            this.consensusSettings = consensusSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.peerTipsByHash = new Dictionary<uint256, HashSet<int>>();
            this.chainedHeadersByHash = new Dictionary<uint256, ChainedHeader>();

            this.lockObject = new object();
        }

        public void Initialize(ChainedHeader chainedHeader)
        {
            ChainedHeader current = chainedHeader;
            while (current.Previous != null)
            {
                current.Previous.Next.Add(current);
                this.chainedHeadersByHash.Add(current.HashBlock, current);
                current = current.Previous;
            }

            if (current.HashBlock != this.network.GenesisHash)
            {
                throw new ConsensusException();
            }
        }

        /// <summary>
        /// A new list of headers are presented from a given peer, the headers will try to be connected to the chain.
        /// Headers that are interesting (i.e may extend our consensus tip) will marked to download there full blocks.
        /// </summary>
        /// <remarks>
        /// The headers are assumed to be consecutive in order. 
        /// </remarks>
        /// <param name="networkPeerId">The network id that is presenting this headers.</param>
        /// <param name="headers">The list of headers to connect to the chain tree.</param>
        /// <returns>Indicators of what blocks need to be downloaded.</returns>
        public ConnectedHeaders ConnectNewHeaders(int networkPeerId, List<BlockHeader> headers)
        {
            Guard.NotNull(headers, nameof(headers));

            if (!this.chainedHeadersByHash.ContainsKey(headers.First().HashPrevBlock))
            {
                this.logger.LogTrace("(-)[HEADER_COULD_NOT_CONNECT]");
                throw new ConnectHeaderException();
            }

            this.CreateNewHeaders(headers, out List<ChainedHeader> newChainedHeaders);

            var oldTip = this.SetPeerTip(networkPeerId, headers.Last().GetHash());

            if (oldTip != null)
            {
                this.RemoveChainClaim(networkPeerId, this.chainedHeadersByHash.TryGet(oldTip));
            }

            if (newChainedHeaders.Empty())
            {
                this.logger.LogTrace("(-)[NO_NEW_HEADER]");
                return new ConnectedHeaders();
            }

            ChainedHeader bestTip = this.chainState.ConsensusTip;
            ChainedHeader earliestNewHeader = newChainedHeaders.First();
            ChainedHeader latestNewHeader = newChainedHeaders.Last();

            if (this.HeaderIsWithinCheckpointsOrAssumedValid(earliestNewHeader))
            {
                ChainedHeader currentChainedHeader = latestNewHeader;

                while (currentChainedHeader != earliestNewHeader)
                {
                    if (currentChainedHeader.HashBlock == this.consensusSettings.BlockAssumedValid)
                    {
                        return this.HandleAssumedValid(currentChainedHeader, latestNewHeader);
                    }

                    CheckpointInfo checkpoint = this.checkpoints.GetCheckpoint(currentChainedHeader.Height);
                    if (checkpoint != null)
                    {
                        return this.HandleCheckpoint(currentChainedHeader, latestNewHeader, checkpoint);
                    }

                    currentChainedHeader = currentChainedHeader.Previous;
                }

                this.logger.LogTrace("(-)[CHECKPOINT_NOT_HIT]");
                return new ConnectedHeaders();
            }

            var connectedHeaders = new ConnectedHeaders();

            if (latestNewHeader.ChainWork > bestTip.ChainWork)
            {
                connectedHeaders.DownloadFrom = connectedHeaders.Consumed = latestNewHeader;

                ChainedHeader current = latestNewHeader;
                ChainedHeader next = current;
                while (current != null)
                {
                    if (this.IsHeaderInAValidatedState(current))
                    {
                        connectedHeaders.DownloadTo = next;
                        break;
                    }

                    this.logger.LogTrace("Header marked as BlockRequired '{0}'", current);
                    current.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;

                    next = current;
                    current = current.Previous;
                }
            }

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// The header is AssumedValid, the header and all of its previous headers will be marked as AssumedValid.
        /// </summary>
        /// <remarks>
        /// If the header's commutative work is better then <see cref="IChainState.ConsensusTip"/> the header and all its predecessors will be marked with <see cref="BlockDataAvailabilityState.BlockRequired"/>.
        /// Once a previous header that is already marked as <see cref="ValidationState.AssumedValid"/> is found then we stop iterating over previous headers.
        /// </remarks>
        /// <param name="assumedValidHeader">The header that is assumed to be valid.</param>
        /// <param name="latestNewHeader">The last header in the list of presented new headers.</param>
        private ConnectedHeaders HandleAssumedValid(ChainedHeader assumedValidHeader, ChainedHeader latestNewHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(assumedValidHeader), assumedValidHeader);

            ChainedHeader bestTip = this.chainState.ConsensusTip;
            var connectedHeaders = new ConnectedHeaders();

            bool newHeaderWorkIsHigher = false;
            if (latestNewHeader.ChainWork > bestTip.ChainWork)
            {
                newHeaderWorkIsHigher = true;
                connectedHeaders.DownloadFrom = connectedHeaders.Consumed = latestNewHeader;
                latestNewHeader.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;
                this.logger.LogTrace("Found a header with more chain work '{0}'", latestNewHeader);

                ChainedHeader currentBlockRequired = latestNewHeader;
                while (currentBlockRequired != assumedValidHeader)
                {
                    currentBlockRequired.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;
                    currentBlockRequired = currentBlockRequired.Previous;
                }
            }

            ChainedHeader current = assumedValidHeader;
            ChainedHeader next = current;
            while (current != null)
            {
                if (this.IsHeaderInAValidatedState(current))
                {
                    if (newHeaderWorkIsHigher)
                    {
                        connectedHeaders.DownloadTo = next;
                    }

                    break;
                }

                this.logger.LogTrace("Header marked as AssumedValid '{0}'", current);
                current.BlockValidationState = ValidationState.AssumedValid;

                if (newHeaderWorkIsHigher)
                {
                    current.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;
                }

                next = current;
                current = current.Previous;
            }
            
            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// When a header is before the last checkpoint it will be asked to be marked as AssumedValid and we'll ask to download it.
        /// </summary>
        private ConnectedHeaders HandleCheckpoint(ChainedHeader chainedHeader, ChainedHeader latestNewHeader, CheckpointInfo checkpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            if (checkpoint.Hash != chainedHeader.HashBlock)
            {
                this.logger.LogTrace("(-)[INVALID_HEADER_NOT_MATCHING_CHECKPOINT]");
                throw new InvalidHeaderException();
            }

            var connectedHeaders = new ConnectedHeaders();

            ChainedHeader subchainTip = chainedHeader;
            if (chainedHeader.Height == this.checkpoints.GetLastCheckpointHeight())
                subchainTip = latestNewHeader;

            connectedHeaders.DownloadFrom = connectedHeaders.Consumed = subchainTip;

            ChainedHeader current = subchainTip;
            ChainedHeader next = current;
            while (current != null)
            {
                if (this.IsHeaderInAValidatedState(current))
                {
                    connectedHeaders.DownloadTo = next;
                    break;
                }

                this.logger.LogTrace("Header marked as AssumedValid '{0}'", current);
                current.BlockValidationState = ValidationState.AssumedValid;

                current.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;

                next = current;
                current = current.Previous;
            }

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// Check whether a header is in one of the following states <see cref="ValidationState.AssumedValid"/>, <see cref="ValidationState.PartiallyValidated"/>, <see cref="ValidationState.FullyValidated"/>. 
        /// </summary>
        private bool IsHeaderInAValidatedState(ChainedHeader chainedHeader)
        {
            if (chainedHeader.BlockValidationState == ValidationState.AssumedValid
                || chainedHeader.BlockValidationState == ValidationState.PartiallyValidated
                || chainedHeader.BlockValidationState == ValidationState.FullyValidated)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// If checkpoints are enabled, check whether the header is before the last checkpoint or if its part of the assumed valid chain.
        /// </summary>
        /// <param name="chainedHeader">the header to check.</param>
        /// <returns><c>true</c> if inside checkpoints or assumed valid.</returns>
        private bool HeaderIsWithinCheckpointsOrAssumedValid(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            if ((this.consensusSettings.UseCheckpoints && chainedHeader.Height > this.checkpoints.GetLastCheckpointHeight()) || this.consensusSettings.BlockAssumedValid != null)
            {
                this.logger.LogTrace("(-):true");
                return true;
            }

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Remove the peer's tip and all the headers claimed by this peer unless they are also claimed by other peers.
        /// Headers that are parents of <see cref="stopHeader"/> will stay in the tree.
        /// </summary>
        /// <param name="networkPeerId">The peer id that is removed.</param>
        /// <param name="peerTip">The peers header tip.</param>
        /// <param name="stopHeader">A stop header that indicates that all headers that are previous should remain in the tree.</param>
        /// <param name="removeAllPeers">A flat which if <c>true</c> will remove all peers.</param>
        private void RemoveChainClaim(int networkPeerId, ChainedHeader peerTip, ChainedHeader stopHeader = null, bool removeAllPeers = false)
        {
            this.logger.LogTrace("({0}:'{1}',{0}:'{1}',{0}:'{1}')", nameof(networkPeerId), networkPeerId, nameof(stopHeader), stopHeader);

            // TODO: what if peer tip is not correct? why not dicover peer tip inside the mothod, 
            // TODO: is there a reason we might traveres a tip that is not the tip of the peer

            ChainedHeader currentHeader = peerTip;
            while (currentHeader != stopHeader)
            {
                bool headerHasNextHeader = currentHeader.Next.Count != 0;
                bool headerIsAPeerTip = false;

                var listOfPeersClaimingThisHeader = this.peerTipsByHash.TryGet(currentHeader.HashBlock);

                if (listOfPeersClaimingThisHeader != null)
                {
                    if (removeAllPeers)
                    {
                        this.logger.LogTrace("Removed all networkPeers.");
                        listOfPeersClaimingThisHeader.Clear();
                    }
                    else
                    {
                        this.logger.LogTrace("Removed networkPeerId = '{0}' .", networkPeerId);
                        listOfPeersClaimingThisHeader.Remove(networkPeerId);
                    }

                    if (listOfPeersClaimingThisHeader.Count == 0)
                    {
                        this.logger.LogTrace("Header is not the tip of a peer, tip = '{0}' .", currentHeader);
                        this.peerTipsByHash.Remove(currentHeader.HashBlock);
                    }
                    else
                    {
                        headerIsAPeerTip = true;
                    }
                }

                if (headerIsAPeerTip || headerHasNextHeader)
                    break;

                this.logger.LogTrace("Header removed from tree, header = '{0}' .", currentHeader);
                this.chainedHeadersByHash.Remove(currentHeader.HashBlock);
                currentHeader.Previous.Next.Remove(currentHeader);

                currentHeader = currentHeader.Previous;

                if (currentHeader.Next.Count != 0)
                    break;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Set a new header as a tip for this peer and remove the old tip.
        /// </summary>
        /// <param name="networkPeerId">The peer id that sets a new tip.</param>
        /// <param name="newTip">The new tip to set.</param>
        /// <returns>The old tip.</returns>
        private uint256 SetPeerTip(int networkPeerId, uint256 newTip)
        {
            this.logger.LogTrace("({0}:'{1}',{0}:'{1}')", nameof(networkPeerId), networkPeerId, nameof(newTip), newTip);

            uint256 oldTip = null;

            List<uint256> hashesToRemove = new List<uint256>();

            foreach (var tipItem in this.peerTipsByHash)
            {
                HashSet<int> listOfPeers = tipItem.Value;
                if (listOfPeers.Contains(networkPeerId))
                {
                    oldTip = tipItem.Key;

                    this.logger.LogTrace("Peer id removed '{0}' for tip header '{1}'", networkPeerId, oldTip);
                    listOfPeers.Remove(networkPeerId);

                    if (listOfPeers.Count == 0)
                    {
                        hashesToRemove.Add(tipItem.Key);
                    }
                }
            }

            foreach (var hash in hashesToRemove)
            {
                this.logger.LogTrace("Header tip removed '{0}'", hash);
                this.peerTipsByHash.Remove(hash);
            }

            var currentTips = this.peerTipsByHash.TryGet(newTip);

            if (currentTips == null)
            {
                currentTips = new HashSet<int>();
                this.peerTipsByHash.Add(newTip, currentTips);
            }

            currentTips.Add(networkPeerId);

            this.logger.LogTrace("(-) '{0}'", oldTip);
            return oldTip;
        }

        /// <summary>
        /// Find the headers that are not part of the tree and try to connect them to an existing chain 
        /// by creating a new <see cref="ChainedHeader"/> type and linking it to its previous header.
        /// </summary>
        /// <remarks>
        /// A new header will perform partial validation.
        /// This will take in to account the MaxReorg protection rule, chains that are beyond the max reorg flag will be abandoned.
        /// This will append to the ChainedHeader.Next of the previous header.
        /// </remarks>
        /// <param name="headers">The new headers that should be connected to a chain.</param>
        /// <param name="newChainedHeaders">The connected headers.</param>
        /// <returns>A list of newly created <see cref="ChainedHeader"/>.</returns>
        private void CreateNewHeaders(List<BlockHeader> headers, out List<ChainedHeader> newChainedHeaders)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(headers), headers.Count);

            newChainedHeaders = new List<ChainedHeader>();
            bool newChainedHeaderFound = false;
            bool verifyMaxReorgViolation = true;
            ChainedHeader previousChainedHeader = null;

            foreach (BlockHeader currentBlockHeader in headers)
            {
                var currentBlockHash = currentBlockHeader.GetHash();

                if (!newChainedHeaderFound && !this.chainedHeadersByHash.ContainsKey(currentBlockHash))
                {
                    this.logger.LogTrace("New header that is not connected to the tree was found '{0}' .", currentBlockHash);
                    newChainedHeaderFound = true;
                }

                if (newChainedHeaderFound)
                {
                    if (previousChainedHeader == null)
                    {
                        this.logger.LogTrace("Find the previous chained header '{0}' .", currentBlockHeader.HashPrevBlock);
                        previousChainedHeader = this.chainedHeadersByHash.TryGet(currentBlockHeader.HashPrevBlock);

                        if (previousChainedHeader == null)
                        {
                            this.logger.LogTrace("(-)[PREVIOUS_HEADER_NOT_FOUND]");
                            throw new ConnectHeaderException();
                        }
                    }

                    ChainedHeader newChainedHeader = new ChainedHeader(currentBlockHeader, currentBlockHash, previousChainedHeader);

                    this.chainedHeaderValidator.Validate(newChainedHeader);

                    if (verifyMaxReorgViolation)
                    {
                        ChainedHeader forkDistanceFromConsensusTip = this.chainState.ConsensusTip.FindFork(newChainedHeader);

                        if (this.IsMaxReorgRuleViolated(forkDistanceFromConsensusTip))
                        {
                            this.logger.LogTrace("(-)[MAX_REORG_VIOLATION]");
                            throw new MaxReorgViolationException();
                        }

                        verifyMaxReorgViolation = false;
                    }

                    newChainedHeaders.Add(newChainedHeader);
                    previousChainedHeader.Next.Add(newChainedHeader);
                    this.chainedHeadersByHash.Add(newChainedHeader.HashBlock, newChainedHeader);

                    previousChainedHeader = newChainedHeader;
                    this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);
                }
            }

            this.logger.LogTrace("({0}:'{1}')", nameof(newChainedHeaders), newChainedHeaders.Count);
        }

        /// <summary>
        /// Checks if <paramref name="tip"/> violates the max reorg rule, if networks.Consensus.MaxReorgLength is zero this logic is disabled.
        /// </summary>
        /// <param name="tip">The tip.</param>
        /// <returns><c>true</c> if maximum reorg rule violated, <c>false</c> otherwise.</returns>
        private bool IsMaxReorgRuleViolated(ChainedHeader tip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(tip), tip);

            uint maxReorgLength = this.chainState.MaxReorgLength;
            ChainedHeader consensusTip = this.chainState.ConsensusTip;
            if ((maxReorgLength != 0) && (consensusTip != null))
            {
                ChainedHeader fork = tip.FindFork(consensusTip);

                if ((fork != null) && (fork != consensusTip))
                {
                    int reorgLength = consensusTip.Height - fork.Height;

                    if (reorgLength > maxReorgLength)
                    {
                        this.logger.LogTrace("Reorganization of length {0} prevented, maximal reorganization length is {1}, consensus tip is '{2}'.", reorgLength, maxReorgLength, consensusTip);
                        this.logger.LogTrace("(-):true");
                        return true;
                    }

                    this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                }
            }

            this.logger.LogTrace("(-):false");
            return false;
        }
    }
}
