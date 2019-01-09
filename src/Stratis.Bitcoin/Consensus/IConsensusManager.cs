using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// TODO add a big nice comment.
    /// </summary>
    public interface IConsensusManager : IDisposable
    {
        /// <summary>The current tip of the chain that has been validated.</summary>
        ChainedHeader Tip { get; }

        /// <summary>The collection of rules.</summary>
        IConsensusRuleEngine ConsensusRules { get; }

        /// <summary>
        /// Set the tip of <see cref="ConsensusManager"/>, if the given <paramref name="chainTip"/> is not equal to <see cref="Tip"/>
        /// then rewind consensus until a common header is found.
        /// </summary>
        /// <param name="chainTip">Last common header between chain repository and block store if it's available,
        /// if the store is not available it is the chain repository tip.</param>
        Task InitializeAsync(ChainedHeader chainTip);

        /// <summary>
        /// A list of headers are presented from a given peer,
        /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
        /// </summary>
        /// <param name="peer">The peer that providing the headers.</param>
        /// <param name="headers">The list of new headers.</param>
        /// <param name="triggerDownload">Specifies if the download should be scheduled for interesting blocks.</param>
        /// <returns>Information about consumed headers.</returns>
        /// <exception cref="ConnectHeaderException">Thrown when first presented header can't be connected to any known chain in the tree.</exception>
        /// <exception cref="CheckpointMismatchException">Thrown if checkpointed header doesn't match the checkpoint hash.</exception>
        /// <exception cref="MaxReorgViolationException">Thrown in case maximum reorganization rule is violated.</exception>
        /// <exception cref="ConsensusErrorException">Thrown if header validation failed.</exception>
        ConnectNewHeadersResult HeadersPresented(INetworkPeer peer, List<BlockHeader> headers, bool triggerDownload = true);

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <param name="peerId">The peer that was disconnected.</param>
        void PeerDisconnected(int peerId);

        /// <summary>
        /// Provides block data for the given block hashes.
        /// </summary>
        /// <remarks>
        /// First we check if the block exists in chained header tree, then it check the block store and if it wasn't found there the block will be scheduled for download.
        /// Given callback is called when the block is obtained. If obtaining the block fails the callback will be called with <c>null</c>.
        /// </remarks>
        /// <param name="blockHashes">The block hashes to download.</param>
        /// <param name="onBlockDownloadedCallback">The callback that will be called for each downloaded block.</param>
        Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, OnBlockDownloadedCallback onBlockDownloadedCallback);

        /// <summary>Loads the block data from <see cref="chainedHeaderTree"/> or block store if it's enabled.</summary>
        /// <param name="blockHash">The block hash.</param>
        Task<ChainedHeaderBlock> GetBlockDataAsync(uint256 blockHash);

        /// <summary>
        /// A new block was mined by the node and is attempted to connect to tip.
        /// </summary>
        /// <param name="block">Block that was mined.</param>
        /// <exception cref="ConsensusErrorException">Thrown if header validation failed.</exception>
        /// <exception cref="ConsensusException">Thrown if partial or full validation failed or if full validation wasn't required.</exception>
        /// <returns><see cref="ChainedHeader"/> of a block that was mined.</returns>
        Task<ChainedHeader> BlockMinedAsync(Block block);
    }

    /// <summary>
    /// A delegate that is used to send callbacks when a block is downloaded from the queued requests to downloading blocks.
    /// </summary>
    /// <param name="chainedHeaderBlock">The pair of the block and its chained header.</param>
    public delegate void OnBlockDownloadedCallback(ChainedHeaderBlock chainedHeaderBlock);
}