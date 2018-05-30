using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// TODO comment this interface
    /// </summary>
    public interface IChainedHeaderValidator
    {
        void Validate(ChainedHeader chainedHeader);
    }

    /// <summary>
    /// Tree of chained block headers that are being claimed by the connected peers and the node itself.
    /// It represents all chains we potentially can sync with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// View of the chains that are presented by connected peers might be incomplete because we always
    /// receive only chunk of headers claimed by the peer in one message.
    /// </para>
    /// <para>
    /// It is a role of the consensus manager to decide which of the presented chains is going to be treated as our best chain.
    /// <see cref="ChainedHeaderTree"/> only advices which chains it might be interesting to download.
    /// </para>
    /// <para>
    /// This class is not thread safe and it the role of the component that uses this class to prevent race conditions.
    /// </para>
    /// </remarks>
    public sealed class ChainedHeaderTree
    {
        private readonly Network network;
        private readonly IChainedHeaderValidator chainedHeaderValidator;
        private readonly ILogger logger;
        private readonly ICheckpoints checkpoints;
        private readonly IChainState chainState;
        private readonly ConsensusSettings consensusSettings;

        /// <summary>A special peer identifier that represents our local node.</summary>
        internal const int LocalPeerId = -1;

        /// <summary>Lists of peer identifiers mapped by hashes of the block headers that are considered to be their tips.</summary>
        /// <remarks>
        /// During the consensus tip changing process, which includes both the reorganization and advancement on the same chain,
        /// it happens that there are two entries for <see cref="LocalPeerId"/>. This means that two different blocks are being
        /// claimed by our node as its tip. This is necessary in order to protect the new consensus tip candidate from being
        /// removed in case peers that were claiming it disconnect during the consensus tip changing process.  
        /// <para>
        /// All the leafs of the tree have to be tips of chains presented by peers, which means that
        /// hashes of the leaf block headers have to be keys with non-empty values in this dictionary.
        /// </para>
        /// </remarks>
        private readonly Dictionary<uint256, HashSet<int>> peerIdsByTipHash;

        /// <summary>A list of peer identifiers that are mapped to their tips.</summary>
        private readonly Dictionary<int, uint256> peerTipsByPeerId;

        /// <summary>
        /// Chained headers mapped by their hashes.
        /// Every chained header that is connected to the tree has to have its hash in this dictionary.
        /// </summary>
        private readonly Dictionary<uint256, ChainedHeader> chainedHeadersByHash;

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

            this.peerTipsByPeerId = new Dictionary<int, uint256>();
            this.peerIdsByTipHash = new Dictionary<uint256, HashSet<int>>();
            this.chainedHeadersByHash = new Dictionary<uint256, ChainedHeader>();
        }

        /// <summary>
        /// Initialize the tree with consensus tip.
        /// </summary>
        /// <param name="consensusTip">The consensus tip.</param>
        public void Initialize(ChainedHeader consensusTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(consensusTip), consensusTip);

            ChainedHeader current = consensusTip;
            while (current.Previous != null)
            {
                current.Previous.Next.Add(current);
                this.chainedHeadersByHash.Add(current.HashBlock, current);
                current = current.Previous;
            }

            // Add the genesis block.
            this.chainedHeadersByHash.Add(current.HashBlock, current);

            if (current.HashBlock != this.network.GenesisHash)
            {
                this.logger.LogTrace("(-)[INVALID_NETWORK]");
                throw new ConsensusException("INVALID_NETWORK");
            }

            this.AddOrReplacePeerTip(LocalPeerId, consensusTip.HashBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A new list of headers are presented by a peer, the headers will try to be connected to the tree.
        /// Blocks associated with headers that are interesting (i.e. represent a chain with greater chainwork than our consensus tip)
        /// will be requested for download.
        /// </summary>
        /// <remarks>
        /// The headers are assumed to be in consecutive order.
        /// </remarks>
        /// <param name="networkPeerId">Id of a peer that presented the headers.</param>
        /// <param name="headers">The list of headers to connect to the tree.</param>
        /// <returns>
        /// Information about which blocks need to be downloaded together with information about which input headers were processed.
        /// Only headers that we can validate will be processed. The rest of the headers will be submitted later again for processing.
        /// </returns>
        public ConnectedHeaders ConnectNewHeaders(int networkPeerId, List<BlockHeader> headers)
        {
            Guard.NotNull(headers, nameof(headers));
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(networkPeerId), networkPeerId, nameof(headers), nameof(headers.Count), headers.Count);

            if (!this.chainedHeadersByHash.ContainsKey(headers[0].HashPrevBlock))
            {
                this.logger.LogTrace("(-)[HEADER_COULD_NOT_CONNECT]");
                throw new ConnectHeaderException();
            }

            List<ChainedHeader> newChainedHeaders = this.CreateNewHeaders(headers);

            this.AddOrReplacePeerTip(networkPeerId, headers.Last().GetHash());

            if (newChainedHeaders == null)
            {
                uint256 lastHash = headers.Last().GetHash();

                this.logger.LogTrace("(-)[NO_NEW_HEADERS]");
                return new ConnectedHeaders() { Consumed = this.chainedHeadersByHash[lastHash] };
            }

            ChainedHeader earliestNewHeader = newChainedHeaders.First();
            ChainedHeader latestNewHeader = newChainedHeaders.Last();

            ConnectedHeaders connectedHeaders = null;

            bool isAssumedValidEnabled = this.consensusSettings.BlockAssumedValid != null;
            bool isBelowLastCheckpoint = this.consensusSettings.UseCheckpoints && (earliestNewHeader.Height <= this.checkpoints.GetLastCheckpointHeight());

            if (isBelowLastCheckpoint || isAssumedValidEnabled)
            {
                ChainedHeader currentChainedHeader = latestNewHeader;

                // When we look for a checkpoint header or an assume valid header, we go from the last presented
                // header to the first one in the reverse order because if there were multiple checkpoints or a checkpoint
                // and an assume valid header inside of the presented list of headers, we would only be interested in the last
                // one as it would cover all previous headers. Reversing the order of processing guarantees that we only need
                // to deal with one special header, which simplifies the implementation.
                while (currentChainedHeader != earliestNewHeader)
                {
                    if (currentChainedHeader.HashBlock == this.consensusSettings.BlockAssumedValid)
                    {
                        this.logger.LogDebug("Chained header '{0}' represents an assumed valid block.", currentChainedHeader);

                        connectedHeaders = this.HandleAssumedValidHeader(currentChainedHeader, latestNewHeader, isBelowLastCheckpoint);
                        break;
                    }

                    CheckpointInfo checkpoint = this.checkpoints.GetCheckpoint(currentChainedHeader.Height);
                    if (checkpoint != null)
                    {
                        this.logger.LogDebug("Chained header '{0}' is a checkpoint.", currentChainedHeader);

                        connectedHeaders = this.HandleCheckpointsHeader(currentChainedHeader, latestNewHeader, checkpoint);
                        break;
                    }

                    currentChainedHeader = currentChainedHeader.Previous;
                }

                if ((connectedHeaders == null) && isBelowLastCheckpoint)
                {
                    connectedHeaders = new ConnectedHeaders() {Consumed = latestNewHeader};
                    this.logger.LogTrace("Chained header '{0}' below last checkpoint.", currentChainedHeader);
                }

                if (connectedHeaders != null)
                {
                    this.logger.LogTrace("(-)[CHECKPOINT_OR_ASSUMED_VALID]:{0}", connectedHeaders);
                    return connectedHeaders;
                }
            }

            if (latestNewHeader.ChainWork > this.chainState.ConsensusTip.ChainWork)
            {
                this.logger.LogDebug("Chained header '{0}' is the tip of a chain with more work than our current consensus tip.", latestNewHeader);

                connectedHeaders = this.MarkBetterChainAsRequired(latestNewHeader);
            }

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// A chain with more work than our current consensus tip was found so mark all it's descendants as required.
        /// </summary>
        /// <param name="latestNewHeader">The new header that represents a longer chain.</param>
        /// <returns>The new headers that need to be downloaded.</returns>
        private ConnectedHeaders MarkBetterChainAsRequired(ChainedHeader latestNewHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(latestNewHeader), latestNewHeader);

            var connectedHeaders = new ConnectedHeaders();
            connectedHeaders.DownloadTo = connectedHeaders.Consumed = latestNewHeader;

            ChainedHeader current = latestNewHeader;
            ChainedHeader next = current;

            while (!this.HeaderWasRequested(current))
            {
                current.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;

                next = current;
                current = current.Previous;
            }

            connectedHeaders.DownloadFrom = next;

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// Mark the chain ending with <paramref name="chainedHeader"/> as <see cref="ValidationState.AssumedValid"/>.
        /// </summary>
        /// <param name="chainedHeader">Last <see cref="ChainedHeader"/> to be marked <see cref="ValidationState.AssumedValid"/>.</param>
        private void MarkTrustedChainAsAssumedValid(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader current = chainedHeader;

            while (!this.HeaderWasMarkedAsValidated(current))
            {
                current.BlockValidationState = ValidationState.AssumedValid;
                current = current.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// The header is assumed to be valid, the header and all of its previous headers will be marked as <see cref="ValidationState.AssumedValid" />.
        /// If the header's cumulative work is better then <see cref="IChainState.ConsensusTip" /> the header and all its predecessors will be marked with <see cref="BlockDataAvailabilityState.BlockRequired" />.
        /// </summary>
        /// <param name="assumedValidHeader">The header that is assumed to be valid.</param>
        /// <param name="latestNewHeader">The last header in the list of presented new headers.</param>
        /// <param name="isBelowLastCheckpoint">Set to <c>true</c> if <paramref name="assumedValidHeader"/> is below the last checkpoint,
        /// <c>false</c> otherwise or if checkpoints are disabled.</param>
        private ConnectedHeaders HandleAssumedValidHeader(ChainedHeader assumedValidHeader, ChainedHeader latestNewHeader, bool isBelowLastCheckpoint)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:{5})", nameof(assumedValidHeader), assumedValidHeader, nameof(latestNewHeader), latestNewHeader, nameof(isBelowLastCheckpoint), isBelowLastCheckpoint);

            ChainedHeader bestTip = this.chainState.ConsensusTip;
            var connectedHeaders = new ConnectedHeaders() {Consumed = latestNewHeader};

            if (latestNewHeader.ChainWork > bestTip.ChainWork)
            {
                this.logger.LogDebug("Chained header '{0}' is the tip of a chain with more work than our current consensus tip.", latestNewHeader);

                ChainedHeader latestHeaderToMark = isBelowLastCheckpoint ? assumedValidHeader : latestNewHeader;
                connectedHeaders = this.MarkBetterChainAsRequired(latestHeaderToMark);
            }

            this.MarkTrustedChainAsAssumedValid(assumedValidHeader);

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// When a header is checkpointed and has a correct hash, chain that ends with such a header
        /// will be marked as <see cref="ValidationState.AssumedValid" /> and requested for download.
        /// </summary>
        /// <param name="chainedHeader">Checkpointed header.</param>
        /// <param name="latestNewHeader">The latest new header that was presented by the peer.</param>
        /// <param name="checkpoint">Information about the checkpoint at the height of the <paramref name="chainedHeader"/>.</param>
        private ConnectedHeaders HandleCheckpointsHeader(ChainedHeader chainedHeader, ChainedHeader latestNewHeader, CheckpointInfo checkpoint)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}.{5}:'{6}')", nameof(chainedHeader), chainedHeader, nameof(latestNewHeader), latestNewHeader, nameof(checkpoint), nameof(checkpoint.Hash), checkpoint.Hash);

            if (checkpoint.Hash != chainedHeader.HashBlock)
            {
                this.logger.LogDebug("Chained header '{0}' does not match checkpoint '{1}'.", chainedHeader, checkpoint.Hash);
                this.logger.LogTrace("(-)[INVALID_HEADER_NOT_MATCHING_CHECKPOINT]");
                throw new InvalidHeaderException();
            }

            ChainedHeader subchainTip = chainedHeader;
            if (chainedHeader.Height == this.checkpoints.GetLastCheckpointHeight())
                subchainTip = latestNewHeader;

            ConnectedHeaders connectedHeaders = this.MarkBetterChainAsRequired(subchainTip);
            this.MarkTrustedChainAsAssumedValid(chainedHeader);

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// Check whether a header is in one of the following states
        /// <see cref="BlockDataAvailabilityState.BlockAvailable"/>, <see cref="BlockDataAvailabilityState.BlockRequired"/>.
        /// </summary>
        private bool HeaderWasRequested(ChainedHeader chainedHeader)
        {
            return (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable)
                  || (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockRequired);
        }

        /// <summary>
        /// Check whether a header is in one of the following states
        /// <see cref="ValidationState.AssumedValid"/>, <see cref="ValidationState.PartiallyValidated"/>, <see cref="ValidationState.FullyValidated"/>.
        /// </summary>
        private bool HeaderWasMarkedAsValidated(ChainedHeader chainedHeader)
        {
            return (chainedHeader.BlockValidationState == ValidationState.AssumedValid)
                  || (chainedHeader.BlockValidationState == ValidationState.PartiallyValidated)
                  || (chainedHeader.BlockValidationState == ValidationState.FullyValidated);
        }

        /// <summary>
        /// Remove branches of the tree that are not claimed by any peer. 
        /// </summary>
        private void RemoveUnclaimedBranch(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader currentHeader = chainedHeader;
            while (true)
            {
                // If current header is an ancestor of some other tip claimed by a peer, do nothing.
                bool headerHasDecendents = currentHeader.Next.Count > 0;
                if (headerHasDecendents)
                {
                    this.logger.LogTrace("Header '{0}' is part of another branch.", currentHeader);
                    break;
                }

                bool headerIsClaimedByPeer = this.peerIdsByTipHash.ContainsKey(chainedHeader.HashBlock);
                if (headerIsClaimedByPeer)
                {
                    this.logger.LogTrace("Header '{0}' is claimed by a peer and won't be removed.", currentHeader);
                    break;
                }
                
                this.chainedHeadersByHash.Remove(currentHeader.HashBlock);
                currentHeader.Previous.Next.Remove(currentHeader);
                this.logger.LogTrace("Header '{0}' was removed from the tree.", currentHeader);

                currentHeader = currentHeader.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Remove the peer's tip and all the headers claimed by this peer unless they are also claimed by other peers.
        /// </summary>
        /// <param name="networkPeerId">The peer id that is removed.</param>
        /// <param name="chainedHeader">The header where we start walking back the chain from.</param>
        private void RemovePeerClaim(int networkPeerId, ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(chainedHeader), chainedHeader);

            // Collection of peer IDs that claim this chained header as their tip.
            HashSet<int> peerIds = this.peerIdsByTipHash.TryGet(chainedHeader.HashBlock);

            if (peerIds == null)
            {
                this.logger.LogTrace("(-)[PEER_TIP_NOT_FOUND]");
                throw new ConsensusException("PEER_TIP_NOT_FOUND");
            }

            this.logger.LogTrace("Tip claim of peer ID {0} removed from chained header '{1}'.", networkPeerId, chainedHeader);
            peerIds.Remove(networkPeerId);

            if (peerIds.Count == 0)
            {
                this.logger.LogTrace("Header '{0}' is not the tip of any peer.", chainedHeader);
                this.peerIdsByTipHash.Remove(chainedHeader.HashBlock);
                this.RemoveUnclaimedBranch(chainedHeader);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Set a new header as a tip for this peer and remove the old tip.
        /// </summary>
        /// <param name="networkPeerId">The peer id that sets a new tip.</param>
        /// <param name="newTip">The new tip to set.</param>
        private void AddOrReplacePeerTip(int networkPeerId, uint256 newTip)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(newTip), newTip);

            uint256 oldTipHash = this.peerTipsByPeerId.TryGet(networkPeerId);

            HashSet<int> listOfPeersClaimingThisHeader = this.peerIdsByTipHash.TryGet(newTip);
            if (listOfPeersClaimingThisHeader == null)
            {
                listOfPeersClaimingThisHeader = new HashSet<int>();
                this.peerIdsByTipHash.Add(newTip, listOfPeersClaimingThisHeader);
            }

            listOfPeersClaimingThisHeader.Add(networkPeerId);
            this.peerTipsByPeerId.AddOrReplace(networkPeerId, newTip);

            if (oldTipHash != null)
            {
                ChainedHeader oldTip = this.chainedHeadersByHash.TryGet(oldTipHash);
                this.RemovePeerClaim(networkPeerId, oldTip);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Find the headers that are not part of the tree and try to connect them to an existing chain 
        /// by creating new chained headers and linking them to their previous headers.
        /// </summary>
        /// <remarks>
        /// Header validation is performed on each header.
        /// It will check if the first header violates maximum reorganization rule.
        /// <para>When headers are connected the next pointers of their previous headers are updated.</para>  
        /// </remarks>
        /// <param name="headers">The new headers that should be connected to a chain.</param>
        /// <returns>A list of newly created chained headers or <c>null</c> if no new headers were found.</returns>
        /// <exception cref="MaxReorgViolationException">Thrown in case maximum reorganization rule is violated.</exception>
        /// <exception cref="ConnectHeaderException">Thrown if it wasn't possible to connect the first new header.</exception>
        private List<ChainedHeader> CreateNewHeaders(List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headers), nameof(headers.Count), headers.Count);

            if (!this.TryFindNewHeaderIndex(headers, out int newHeaderIndex))
            {
                this.logger.LogTrace("(-)[NO_NEW_HEADERS_FOUND]:null");
                return null;
            }

            ChainedHeader previousChainedHeader;
            if (!this.chainedHeadersByHash.TryGetValue(headers[newHeaderIndex].HashPrevBlock, out previousChainedHeader))
            {
                this.logger.LogTrace("Previous hash `{0}` of block hash `{1}` was not found.", headers[newHeaderIndex].GetHash(), headers[newHeaderIndex].HashPrevBlock);
                this.logger.LogTrace("(-)[PREVIOUS_HEADER_NOT_FOUND]");
                throw new ConnectHeaderException();
            }

            var newChainedHeaders = new List<ChainedHeader>();

            ChainedHeader newChainedHeader = this.CreateAndValidateNewChainedHeader(headers[newHeaderIndex], previousChainedHeader);
            newChainedHeaders.Add(newChainedHeader);
            newHeaderIndex++;

            this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);

            try
            {
                this.CheckMaxReorgRuleViolated(newChainedHeader);

                previousChainedHeader = newChainedHeader;

                for (; newHeaderIndex < headers.Count; newHeaderIndex++)
                {
                    newChainedHeader = this.CreateAndValidateNewChainedHeader(headers[newHeaderIndex], previousChainedHeader);
                    newChainedHeaders.Add(newChainedHeader);
                    this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);

                    previousChainedHeader = newChainedHeader;
                }
            }
            catch
            {
                // Undo changes to the tree. This is necessary because the peer claim wasn't set to the last header yet.
                // So in case of peer disconnection this branch wouldn't be removed.
                this.RemoveUnclaimedBranch(newChainedHeader);

                this.logger.LogTrace("(-)[VALIDATION_FAILED]");
                throw;
            }

            this.logger.LogTrace("(-):*.{0}:{1}", nameof(newChainedHeaders.Count), newChainedHeaders.Count);
            return newChainedHeaders;
        }

        private ChainedHeader CreateAndValidateNewChainedHeader(BlockHeader currentBlockHeader, ChainedHeader previousChainedHeader)
        {
            var newChainedHeader = new ChainedHeader(currentBlockHeader, currentBlockHeader.GetHash(), previousChainedHeader);

            this.chainedHeaderValidator.Validate(newChainedHeader);

            previousChainedHeader.Next.Add(newChainedHeader);
            this.chainedHeadersByHash.Add(newChainedHeader.HashBlock, newChainedHeader);

            return newChainedHeader;
        }

        /// <summary>
        /// Find the first header in the given list of <see cref="headers"/> that does not exist in <see cref="chainedHeadersByHash"/>.
        /// </summary>
        private bool TryFindNewHeaderIndex(List<BlockHeader> headers, out int newHeaderIndex)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headers), nameof(headers.Count), headers.Count);

            for (newHeaderIndex = 0; newHeaderIndex < headers.Count; newHeaderIndex++)
            {
                uint256 currentBlockHash = headers[newHeaderIndex].GetHash();
                if (!this.chainedHeadersByHash.ContainsKey(currentBlockHash))
                {
                    this.logger.LogTrace("A new header with hash '{0}' was found that is not connected to the tree.", currentBlockHash);
                    this.logger.LogTrace("(-):true,{0}:{1}", nameof(newHeaderIndex), newHeaderIndex);
                    return true;
                }
            }

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Checks if <paramref name="chainedHeader"/> violates the max reorg rule, if <see cref="IChainState.MaxReorgLength"/> is zero this logic is disabled.
        /// </summary>
        /// <param name="chainedHeader">The header that needs to be checked for reorg.</param>
        /// <exception cref="MaxReorgViolationException">Thrown in case maximum reorganization rule is violated.</exception>
        private void CheckMaxReorgRuleViolated(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            uint maxReorgLength = this.chainState.MaxReorgLength;
            ChainedHeader consensusTip = this.chainState.ConsensusTip;
            if ((maxReorgLength != 0) && (consensusTip != null))
            {
                ChainedHeader fork = chainedHeader.FindFork(consensusTip);

                if ((fork != null) && (fork != consensusTip))
                {
                    int reorgLength = consensusTip.Height - fork.Height;

                    if (reorgLength > maxReorgLength)
                    {
                        this.logger.LogTrace("Reorganization of length {0} prevented, maximal reorganization length is {1}, consensus tip is '{2}'.", reorgLength, maxReorgLength, consensusTip);
                        this.logger.LogTrace("(-)[MAX_REORG_VIOLATION]");
                        throw new MaxReorgViolationException();
                    }

                    this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                }
            }

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// Represents the result of the <see cref="ChainedHeaderTree.ConnectNewHeaders"/> method.
    /// </summary>
    public class ConnectedHeaders
    {
        /// <summary>The earliest header in the chain of the list of headers we are interested in downloading.</summary>
        public ChainedHeader DownloadFrom { get; set; }

        /// <summary>The latest header in the chain of the list of headers we are interested in downloading.</summary>
        public ChainedHeader DownloadTo { get; set; }

        /// <summary>Represents the last processed header from the headers presented by the peer.</summary>
        public ChainedHeader Consumed { get; set; }
        
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.DownloadFrom)}='{this.DownloadFrom}',{nameof(this.DownloadTo)}='{this.DownloadTo}',{nameof(this.Consumed)}='{this.Consumed}'";
        }
    }
}
