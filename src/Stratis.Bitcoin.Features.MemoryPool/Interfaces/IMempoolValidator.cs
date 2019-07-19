using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.MemoryPool.Interfaces
{
    /// <summary>
    /// Public interface for the memory pool validator.
    /// </summary>
    public interface IMempoolValidator
    {
        /// <summary>Gets the proof of work consensus option.</summary>
        ConsensusOptions ConsensusOptions { get; }

        /// <summary>Gets the memory pool performance counter.</summary>
        MempoolPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Accept transaction to memory pool.
        /// Sets the validation state accept time to now.
        /// </summary>
        /// <param name="state">Validation state.</param>
        /// <param name="tx">Transaction to accept.</param>
        /// <returns>Whether the transaction is accepted or not.</returns>
        Task<bool> AcceptToMemoryPool(MempoolValidationState state, Transaction tx);

        /// <summary>
        /// Accept transaction to memory pool.
        /// Honors the validation state accept time.
        /// </summary>
        /// <param name="state">Validation state.</param>
        /// <param name="tx">Transaction to accept.</param>
        /// <returns>Whether the transaction was accepted to the memory pool.</returns>
        Task<bool> AcceptToMemoryPoolWithTime(MempoolValidationState state, Transaction tx);

        /// <summary>
        /// Executes the memory pool sanity check here <see cref="TxMempool.Check(CoinView)"/>.
        /// </summary>
        Task SanityCheck();
    }
}