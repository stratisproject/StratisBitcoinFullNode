using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    /// <summary>
    /// Visitor classes are able to make changes to the underlying consensus manager properties, most importantly the <see cref="ChainedHeaderTree"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of result the visitor will return.</typeparam>
    public interface IConsensusVisitor<T>
    {
        /// <summary>
        /// Accepts a visitor to the consensus manager.
        /// </summary>
        /// <typeparam name="T">The result from visiting consensus.</typeparam>
        /// <param name="consensusManager">The consensus manager instance the visitor is visiting.</param>
        Task<T> VisitAsync(ConsensusManager consensusManager);
    }
}
