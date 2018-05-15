using System;
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
        private readonly ChainState chainState;
        private readonly ConsensusSettings consensusSettings;

        private readonly Dictionary<uint256, List<int>> peerTipsByHash;
        private readonly Dictionary<uint256, ChainedHeader> chainedHeadersByHash;

        private readonly object lockObject;

        public ChainedHeaderTree(
            Network network, 
            ILoggerFactory loggerFactory, 
            IChainedHeaderValidator chainedHeaderValidator, 
            ICheckpoints checkpoints, 
            ChainState chainState, 
            ConsensusSettings consensusSettings)
        {
            this.network = network;
            this.chainedHeaderValidator = chainedHeaderValidator;
            this.checkpoints = checkpoints;
            this.chainState = chainState;
            this.consensusSettings = consensusSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.peerTipsByHash = new Dictionary<uint256, List<int>>();
            this.chainedHeadersByHash = new Dictionary<uint256, ChainedHeader>();

            this.lockObject = new object();
        }

        public ConnectedHeaders ConnectNewHeaders(INetworkPeer networkPeer, List<BlockHeader> headers)
        {
            Guard.NotNull(headers, nameof(headers));

            if (!this.chainedHeadersByHash.ContainsKey(headers.First().HashPrevBlock))
            {
                this.logger.LogTrace("(-)[HEADER_COULD_NOT_CONNECT] '{0}' .", headers.First().HashPrevBlock);
                throw new ConnectHeaderException();
            }

            this.CreateNewHeaders(headers, out List<ChainedHeader> newChainedHeaders);
            
            if (newChainedHeaders.Empty())
            {
                return new ConnectedHeaders();
            }

            this.AddChainClaim(networkPeer.Connection.Id, newChainedHeaders.Last().HashBlock, out uint256 oldTip);
            this.RemoveChainClaim(networkPeer.Connection.Id, oldTip);

            ChainedHeader bestTip = this.chainState.ConsensusTip;
            ChainedHeader firstNewHeader = newChainedHeaders.First();
            ChainedHeader lastNewHeader = newChainedHeaders.Last();

            if (this.HeaderIsWithinCheckpointsOrAssumedValid(firstNewHeader))
            {
                foreach (var chainedHeader in newChainedHeaders)
                {
                    if (chainedHeader.HashBlock == this.consensusSettings.BlockAssumedValid)
                    {
                        return this.HandleAssumedValid(chainedHeader, firstNewHeader, lastNewHeader);
                    }

                    CheckpointInfo checkpoint = this.checkpoints.GetCheckpoint(chainedHeader.Height);
                    if (checkpoint != null)
                    {
                        return this.HandleCheckpoint(chainedHeader, firstNewHeader, lastNewHeader, checkpoint);
                    }
                }

                return new ConnectedHeaders();
            }

            var lastHeader = newChainedHeaders.Last();
            if (lastHeader.ChainWork > bestTip.ChainWork)
            {
                // lastHeader = DataRequired
                return new ConnectedHeaders();
            }

            return new ConnectedHeaders();
        }

        private ConnectedHeaders HandleAssumedValid(ChainedHeader chainedHeader, ChainedHeader firstNewHeader, ChainedHeader lastNewHeader)
        {
            ChainedHeader bestTip = this.chainState.ConsensusTip;

            if (lastNewHeader.ChainWork > bestTip.ChainWork)
            {
                // last.DataRequired;
            }

            ChainedHeader current = chainedHeader;
            while (current != null)
            {
                //if(current == AssumedValid)
                //    break;

                //current.AssumedValid 
                current = current.Previous;
            }

           return new ConnectedHeaders() { DownloadFrom = firstNewHeader, DownloadTo = lastNewHeader, Consumed = chainedHeader };
        }

        private ConnectedHeaders HandleCheckpoint(ChainedHeader chainedHeader, ChainedHeader firstNewHeader, ChainedHeader lastNewHeader, CheckpointInfo checkpoint)
        {
            if (checkpoint.Hash != chainedHeader.HashBlock)
            {
                throw new InvalidHeaderException();
            }

            ChainedHeader subchainTip = chainedHeader;
            if (chainedHeader.Height == this.checkpoints.GetLastCheckpointHeight())
                subchainTip = lastNewHeader;

            ChainedHeader current = subchainTip;
            while (current != null)
            {
                //current.DataRequired

                //if(current == AssumedValid)
                //    break;

                //current.AssumedValid 
                current = current.Previous;
            }

            return new ConnectedHeaders() { DownloadFrom = firstNewHeader, DownloadTo = lastNewHeader, Consumed = chainedHeader };

        }

        public void PeerDisconnect(NetworkPeer networkPeer)
        {
        }

        private bool HeaderIsWithinCheckpointsOrAssumedValid(ChainedHeader chainedHeader)
        {
            if ((this.consensusSettings.UseCheckpoints && chainedHeader.Height > this.checkpoints.GetLastCheckpointHeight()) || this.consensusSettings.BlockAssumedValid != null)
            {
                return true;
            }

            return false;
        }

        private void RemoveChainClaim(int networkPeerId, uint256 headerTip, ChainedHeader stopHeader = null)
        {
            // wait for .Next
        }

        /// <summary>
        /// Add a claim for a network to be on a certain chain tip, and remove the previous claim.
        /// </summary>
        private void AddChainClaim(int networkPeerId, uint256 newTip, out uint256 oldTip)
        {
            this.logger.LogTrace("()");
            oldTip = null;

            foreach (var tipItem in this.peerTipsByHash)
            {
                List<int> peers = tipItem.Value;
                if (peers.Contains(networkPeerId))
                {
                    oldTip = tipItem.Key;
                    peers.Remove(networkPeerId);
                }
            }

            var currentTips = this.peerTipsByHash.TryGet(newTip);

            if (currentTips == null)
            {
                currentTips = new List<int>();
                this.peerTipsByHash.Add(newTip, currentTips);
            }

            currentTips.Add(networkPeerId);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Find the headers that are not part of the tree and try to connect them to existing chains by creating a new <see cref="ChainedHeader"/> type and linking it to its previous header.
        /// </summary>
        /// <remarks>
        /// More then one chain may be created or appended to in the tree.
        /// A new header will perform partial validation.
        /// This will take in to account the MaxReorg protection rule, chains that are beyond the max reorg flag will be abandoned.
        /// This will append to the ChainedHeader.Next of the previous header.
        /// </remarks>
        /// <param name="headers">The new headers that should be connected.</param>
        /// <param name="newChainedHeaders">The connected headers.</param>
        /// <returns>A list of newly created <see cref="ChainedHeader"/>.</returns>
        private void CreateNewHeaders(List<BlockHeader> headers, out List<ChainedHeader> newChainedHeaders)
        {
            this.logger.LogTrace("()");

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
                            this.logger.LogTrace("(-)The previous chained header was not found in the tree '{0}'.", currentBlockHeader.HashPrevBlock);
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
                            throw new MaxReorgViolationException();
                        }

                        verifyMaxReorgViolation = false;
                    }

                    // add to previousChainedHeader.Next

                    previousChainedHeader = newChainedHeader;
                    this.chainedHeadersByHash.Add(newChainedHeader.HashBlock, newChainedHeader);
                    newChainedHeaders.Add(newChainedHeader);
                    this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks if <paramref name="tip"/> violates the max reorg rule, if networks.Consensus.MaxReorgLength is true this logic is disabled.
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
