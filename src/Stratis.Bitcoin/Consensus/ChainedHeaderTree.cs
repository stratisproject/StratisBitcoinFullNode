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
    public interface IChainedHeaderValidator
    {
        void Validate(ChainedHeader chainedHeader);
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

        /// <summary>A special peer identifier that represents our local node.</summary>
        internal const int LocalPeerId = -1;

        /// <summary>A list of headers that represent the tips of peers.</summary>
        private readonly Dictionary<uint256, HashSet<int>> peerIdsByTipHash;

        /// <summary>A list of peer identifiers that map to the peers tip has.</summary>
        private readonly Dictionary<int, uint256> peerTipsByPeerId;

        /// <summary>An indexed collection of <see cref="ChainedHeader"/> that represents a tree of chains.</summary>
        private readonly Dictionary<uint256, ChainedHeader> chainedHeadersByHash;

        internal Dictionary<uint256, ChainedHeader> GetChainedHeadersByHash => this.chainedHeadersByHash;
        internal Dictionary<uint256, HashSet<int>> GetPeerIdsByTipHash => this.peerIdsByTipHash;
        internal Dictionary<int, uint256> GetPeerTipsByPeerId => this.peerTipsByPeerId;

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
        /// Initialize the tree with a <see cref="ChainedHeader"/> that is the tip of our best known consensus chain.
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

            if (current.HashBlock != this.network.GenesisHash)
            {
                this.logger.LogTrace("(-)[INVALID_NETWORK]");
                throw new ConsensusException();
            }

            this.SetPeerTip(LocalPeerId, consensusTip.HashBlock);

            this.logger.LogTrace("(-)");
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
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(networkPeerId), networkPeerId, nameof(headers), nameof(headers.Count), headers.Count);

            if (!this.chainedHeadersByHash.ContainsKey(headers[0].HashPrevBlock))
            {
                this.logger.LogTrace("(-)[HEADER_COULD_NOT_CONNECT]");
                throw new ConnectHeaderException();
            }

            List<ChainedHeader> newChainedHeaders = this.CreateNewHeaders(headers);

            this.SetPeerTip(networkPeerId, headers.Last().GetHash());

            if (newChainedHeaders == null)
            {
                this.logger.LogTrace("(-)[NO_NEW_HEADER]");
                return new ConnectedHeaders();
            }

            ChainedHeader earliestNewHeader = newChainedHeaders.First();
            ChainedHeader latestNewHeader = newChainedHeaders.Last();

            ConnectedHeaders connectedHeaders = null;

            bool isAssumedValidEnabled = this.consensusSettings.BlockAssumedValid != null;
            bool isWithinCheckpoints = this.consensusSettings.UseCheckpoints && (earliestNewHeader.Height <= this.checkpoints.GetLastCheckpointHeight()); ;

            if (isWithinCheckpoints || isAssumedValidEnabled)
            {
                ChainedHeader currentChainedHeader = latestNewHeader;

                while (currentChainedHeader != earliestNewHeader)
                {
                    if (currentChainedHeader.HashBlock == this.consensusSettings.BlockAssumedValid)
                    {
                        this.logger.LogDebug("Chained header '{0}' represents an assumed valid block.", currentChainedHeader);

                        connectedHeaders = this.HandleAssumedValidHeader(currentChainedHeader, latestNewHeader);
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

                if ((connectedHeaders == null) && isWithinCheckpoints)
                {
                    connectedHeaders = new ConnectedHeaders() {Consumed = latestNewHeader};
                    this.logger.LogTrace("Chained header '{0}' bellow last checkpoint.", currentChainedHeader);
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

            ConnectedHeaders connectedHeaders = new ConnectedHeaders();
            connectedHeaders.DownloadTo = connectedHeaders.Consumed = latestNewHeader;

            ChainedHeader current = latestNewHeader;
            ChainedHeader next = current;

            while (this.HeaderWasNotRequested(current))
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
        /// Mark all previous blocks to <see cref="chainedHeader"/> as <see cref="ValidationState.AssumedValid"/>.
        /// </summary>
        /// <param name="chainedHeader">The new header that represents a longer chain.</param>
        private void MarkTrustedChainAsAssumedValid(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader current = chainedHeader;

            while (this.HeaderWasNotMarkedAsValidated(current))
            {
                current.BlockValidationState = ValidationState.AssumedValid;
                current = current.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// The header is assumed to be valid, the header and all of its previous headers will be marked as <see cref="ValidationState.AssumedValid"/>.
        /// If the header's cumulative work is better then <see cref="IChainState.ConsensusTip"/> the header and all its predecessors will be marked with <see cref="BlockDataAvailabilityState.BlockRequired"/>.
        /// </summary>
        /// <param name="assumedValidHeader">The header that is assumed to be valid.</param>
        /// <param name="latestNewHeader">The last header in the list of presented new headers.</param>
        private ConnectedHeaders HandleAssumedValidHeader(ChainedHeader assumedValidHeader, ChainedHeader latestNewHeader)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(assumedValidHeader), assumedValidHeader, nameof(latestNewHeader), latestNewHeader);

            ChainedHeader bestTip = this.chainState.ConsensusTip;
            ConnectedHeaders connectedHeaders = new ConnectedHeaders() {Consumed = latestNewHeader};

            if (latestNewHeader.ChainWork > bestTip.ChainWork)
            {
                this.logger.LogDebug("Chained header '{0}' is the tip of a chain with more work than our current consensus tip.", latestNewHeader);

                connectedHeaders = this.MarkBetterChainAsRequired(latestNewHeader);
            }

            this.MarkTrustedChainAsAssumedValid(assumedValidHeader);

            this.logger.LogTrace("(-):{0}", connectedHeaders);
            return connectedHeaders;
        }

        /// <summary>
        /// When a header is before the last checkpoint it will be asked to be marked as AssumedValid and we'll ask to download it.
        /// </summary>
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
        private bool HeaderWasNotRequested(ChainedHeader chainedHeader)
        {
            return !((chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable)
                  || (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockRequired));
        }

        /// <summary>
        /// Check whether a header is in one of the following states
        /// <see cref="ValidationState.AssumedValid"/>, <see cref="ValidationState.PartiallyValidated"/>, <see cref="ValidationState.FullyValidated"/>.
        /// </summary>
        private bool HeaderWasNotMarkedAsValidated(ChainedHeader chainedHeader)
        {
            return !((chainedHeader.BlockValidationState == ValidationState.AssumedValid)
                  || (chainedHeader.BlockValidationState == ValidationState.PartiallyValidated)
                  || (chainedHeader.BlockValidationState == ValidationState.FullyValidated));
        }

        private void RemoveUnclaimedBranch(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader currentHeader = chainedHeader;
            while (true)
            {
                bool headerHasNextHeader = currentHeader.Next.Count > 0;
                if (headerHasNextHeader)
                {
                    this.logger.LogTrace("Header '{0}' is part of another branch.", currentHeader);
                    break;
                }

                bool headerHasPeerClaim = this.peerIdsByTipHash.ContainsKey(chainedHeader.HashBlock);
                if (headerHasPeerClaim)
                {
                    this.logger.LogTrace("Header '{0}' is claimed by a peer and wont be removed.", currentHeader);
                    break;
                }

                this.logger.LogTrace("Header '{0}' was removed from the chain tree.", currentHeader);
                this.chainedHeadersByHash.Remove(currentHeader.HashBlock);
                currentHeader.Previous.Next.Remove(currentHeader);

                currentHeader = currentHeader.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Remove the peer's tip and all the headers claimed by this peer unless they are also claimed by other peers.
        /// </summary>
        /// <param name="chainedHeader">The header where we start walking back the chain from.</param>
        /// <param name="networkPeerId">The peer id that is removed.</param>
        private void RemovePeerClaim(int networkPeerId, ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(chainedHeader), chainedHeader);

            var listOfPeersClaimingThisHeader = this.peerIdsByTipHash.TryGet(chainedHeader.HashBlock);

            if (listOfPeersClaimingThisHeader == null)
            {
                this.logger.LogTrace("(-)[PEER_TIP_NOT_FOUND]");
                throw new ConsensusException();
            }

            this.logger.LogTrace("Peer id {0 } removed.", networkPeerId);
            listOfPeersClaimingThisHeader.Remove(networkPeerId);

            if (listOfPeersClaimingThisHeader.Count == 0)
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
        private void SetPeerTip(int networkPeerId, uint256 newTip)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(newTip), newTip);

            uint256 oldTipHash = this.peerTipsByPeerId.TryGet(networkPeerId);

            var listOfPeersClaimingThisHeader = this.peerIdsByTipHash.TryGet(newTip);
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
        /// by creating a new <see cref="ChainedHeader"/> type and linking it to its previous header.
        /// </summary>
        /// <remarks>
        /// A new header will perform partial validation.
        /// This will take in to account the MaxReorg protection rule, chains that are beyond the max reorg flag will be abandoned.
        /// This will append to the ChainedHeader.Next of the previous header.
        /// </remarks>
        /// <param name="headers">The new headers that should be connected to a chain.</param>
        /// <returns>A list of newly created <see cref="ChainedHeader"/> or <c>null</c> if no new headers were found.</returns>
        private List<ChainedHeader> CreateNewHeaders(List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headers), nameof(headers.Count), headers.Count);

            if (!this.FindFirstNewHeader(headers, out int newHeaderPosition))
            {
                this.logger.LogTrace("(-)[NO_NEW_HEADERS_FOUND]:null");
                return null;
            }

            ChainedHeader previousChainedHeader = this.chainedHeadersByHash.TryGet(headers[newHeaderPosition].HashPrevBlock);
            if (previousChainedHeader == null)
            {
                this.logger.LogTrace("(-)[PREVIOUS_HEADER_NOT_FOUND]: Previous hash `{0}` of block hash `{1}` was not found.", headers[newHeaderPosition].GetHash(), headers[newHeaderPosition].HashPrevBlock);
                throw new ConnectHeaderException();
            }

            var newChainedHeaders = new List<ChainedHeader>();

            ChainedHeader newChainedHeader = this.CreateAndValidateNewChainedHeader(newChainedHeaders, headers[newHeaderPosition], previousChainedHeader);
            newHeaderPosition++;
            this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);

            this.CheckMaxReorgRuleViolated(newChainedHeader);

            previousChainedHeader = newChainedHeader;

            for (; newHeaderPosition < headers.Count; newHeaderPosition++)
            {
                newChainedHeader = this.CreateAndValidateNewChainedHeader(newChainedHeaders, headers[newHeaderPosition], previousChainedHeader);
                this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);

                previousChainedHeader = newChainedHeader;
            }

            this.logger.LogTrace("(-):*.{0}:{1}", nameof(newChainedHeaders.Count), newChainedHeaders.Count);
            return newChainedHeaders;
        }

        private ChainedHeader CreateAndValidateNewChainedHeader(List<ChainedHeader> newChainedHeaders, BlockHeader currentBlockHeader, ChainedHeader previousChainedHeader)
        {
            var newChainedHeader = new ChainedHeader(currentBlockHeader, currentBlockHeader.GetHash(), previousChainedHeader);

            this.chainedHeaderValidator.Validate(newChainedHeader);

            newChainedHeaders.Add(newChainedHeader);
            previousChainedHeader.Next.Add(newChainedHeader);
            this.chainedHeadersByHash.Add(newChainedHeader.HashBlock, newChainedHeader);

            return newChainedHeader;
        }

        private bool FindFirstNewHeader(List<BlockHeader> headers, out int newHeaderPosition)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headers), nameof(headers.Count), headers.Count);

            for (newHeaderPosition = 0; newHeaderPosition < headers.Count; newHeaderPosition++)
            {
                uint256 currentBlockHash = headers[newHeaderPosition].GetHash();
                if (this.chainedHeadersByHash.ContainsKey(currentBlockHash))
                {
                    this.logger.LogTrace("A new header with hash '{0}' was found that is not connected to the tree.", currentBlockHash);
                    this.logger.LogTrace("(-):true");
                    return true;
                }
            }

            this.logger.LogTrace("(-):false,{0}:{1}", nameof(newHeaderPosition), newHeaderPosition);
            return false;
        }

        /// <summary>
        /// Checks if <paramref name="chainedHeader"/> violates the max reorg rule, if networks.Consensus.MaxReorgLength is zero this logic is disabled.
        /// </summary>
        /// <param name="chainedHeader">The header that needs to be checked for reorg.</param>
        /// <returns><c>true</c> if maximum reorg rule violated, <c>false</c> otherwise.</returns>
        private void CheckMaxReorgRuleViolated(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader tip = this.chainState.ConsensusTip.FindFork(chainedHeader);

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
    /// Represents the response form the <see cref="ChainedHeaderTree.ConnectNewHeaders"/> method.
    /// </summary>
    public class ConnectedHeaders
    {
        /// <summary>The earliest header in the chain of the list of headers we are interested in downloading.</summary>
        public ChainedHeader DownloadFrom { get; set; }

        /// <summary>The latest header in the chain of the list of headers we are interested in downloading.</summary>
        public ChainedHeader DownloadTo { get; set; }

        /// <summary>Represent the chain of headers that are where added to the tree.</summary>
        public ChainedHeader Consumed { get; set; }

        public override string ToString()
        {
            return $"{nameof(this.DownloadFrom)}='{this.DownloadFrom}',{nameof(this.DownloadTo)}='{this.DownloadTo}',{nameof(this.Consumed)}='{this.Consumed}'";
        }
    }
}
