using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.CoinViews
{
    /// <summary>
    /// Database of UTXOs.
    /// </summary>
    public interface ICachedCoinView : ICoinView
    {
        /// <summary>
        /// Persists changes to the coinview when a new block
        /// (hash <paramref name="currentBlock"/>) is added and becomes the new tip of the coinview.
        /// <para>
        /// This method is provided (in <paramref name="unspentOutputs"/> parameter) with information about all
        /// transactions that are either new or were changed in the new block. It is also provided with information
        /// (in <see cref="originalOutputs"/>) about the previous state of those transactions (if any),
        /// which is used for <see cref="ICoinView.Rewind"/> operation.
        /// </para>
        /// </summary>
        /// <param name="unspentOutputs">Information about the changes between the old block and the new block. An item in this list represents a list of all outputs
        /// for a specific transaction. If a specific output was spent, the output is <c>null</c>.</param>
        /// <param name="originalOutputs">Information about the previous state of outputs of transactions inside <paramref name="unspentOutputs"/>. If an item here is <c>null</c>,
        /// it means that the ouputs are newly created in the new block. If it is not <c>null</c>, it holds information about which outputs of the transaction were previously spent
        /// and which were not.</param>
        /// <param name="currentBlock">Block of the current tip of the coinview.</param>
        Task AddRewindDataAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, ChainedHeader currentBlock);

        /// <summary>
        /// Initializes this instance.
        /// </summary>
         void Initialize();
    }
}
