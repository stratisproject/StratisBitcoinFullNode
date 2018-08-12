using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Visitors;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// TODO add a big nice comment.
    /// </summary>
    public interface IConsensusManager : IDisposable
    {
        Task<T> AcceptVisitorAsync<T>(IConsensusVisitor<T> visitor);

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
    }

    /// <summary>
    /// A delegate that is used to send callbacks when a block is downloaded from the of queued requests to downloading blocks.
    /// </summary>
    /// <param name="chainedHeaderBlock">The pair of the block and its chained header.</param>
    public delegate void OnBlockDownloadedCallback(ChainedHeaderBlock chainedHeaderBlock);
}