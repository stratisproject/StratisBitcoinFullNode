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
    public interface IConsensusManager
    {
        /// <summary>The current tip of the chain that has been validated.</summary>
        ChainedHeader Tip { get; }

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
        ConnectNewHeadersResult HeadersPresented(INetworkPeer peer, List<BlockHeader> headers, bool triggerDownload = true);

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <param name="peerId">The peer that was disconnected.</param>
        void OnPeerDisconnected(int peerId);

        /// <summary>
        /// Provides block data for the given block hashes.
        /// </summary>
        /// <remarks>
        /// First we check if the block exists in chained header tree, then it check the block store and if it wasn't found there the block will be scheduled for download.
        /// Given callback is called when the block is obtained. If obtaining the block fails the callback will be called with <c>null</c>.
        /// </remarks>
        /// <param name="blockHashes">The block hashes to download.</param>
        /// <param name="onBlockDownloadedCallback">The callback that will be called for each downloaded block.</param>
        Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, Action<ChainedHeaderBlock> onBlockDownloadedCallback);
    }
}