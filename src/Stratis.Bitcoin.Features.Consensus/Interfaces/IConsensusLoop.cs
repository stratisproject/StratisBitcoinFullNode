using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.Features.Consensus.Interfaces
{
    /// <summary>
    /// Interface for a consensus loop.
    /// Consumes incoming blocks, validates and executes them.
    /// </summary>
    public interface IConsensusLoop
    {
        /// <summary>A chain of headers all the way to genesis.</summary>
        ConcurrentChain Chain { get; }

        /// <summary>Contain information about deployment and activation of features in the chain.</summary>
        NodeDeployments NodeDeployments { get; }

        /// <summary>A puller that can pull blocks from peers on demand.</summary>
        LookaheadBlockPuller Puller { get; }

        /// <summary>Information holding POS data chained.</summary>
        IStakeChain StakeChain { get; }

        /// <summary>The current tip of the chain that has been validated.</summary>
        ChainedBlock Tip { get; }

        /// <summary>The consensus db, containing all unspent UTXO in the chain.</summary>
        CoinView UTXOSet { get; }

        /// <summary>The validation logic for the consensus rules.</summary>
        IPowConsensusValidator Validator { get; }

        /// <summary>
        /// A method that will accept a new block to the node.
        /// The block will be validated and the <see cref="CoinView"/> db will be updated.
        /// If it's a new block that was mined or staked it will extend the chain and the new block will set <see cref="ConcurrentChain.Tip"/>.
        /// </summary>
        /// <param name="blockValidationContext">Information about the block to validate.</param>
        Task AcceptBlockAsync(BlockValidationContext blockValidationContext);

        /// <summary>
        /// Flushes changes in the cached coinview to the disk.
        /// </summary>
        /// <param name="force"><c>true</c> to enforce flush, <c>false</c> to flush only if the cached coinview itself wants to be flushed.</param>
        Task FlushAsync(bool force);

        /// <summary>
        /// Initialize components in <see cref="ConsensusLoop"/>.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Dispose components in <see cref="ConsensusLoop"/>.
        /// </summary>
        void Stop();

        /// <summary>
        /// Validates a block using the consensus rules.
        /// </summary>
        void ValidateBlock(RuleContext context);
    }
}