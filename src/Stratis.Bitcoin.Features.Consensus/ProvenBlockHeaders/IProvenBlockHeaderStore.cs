using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Database of <see cref="ProvenBlockHeader"/>s.
    /// </summary>
    public interface IProvenBlockHeaderStore : IDisposable
    {
        /// <summary>
        /// Loads <see cref="ProvenBlockHeader"/> items from the database.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Load <see cref="ProvenBlockHeader"/> items into the store.
        /// </summary>
        Task LoadAsync();

        /// <summary>
        /// Get a <see cref="ProvenBlockHeader"/> corresponding to a block.
        /// </summary>
        /// <param name="blockHeight">Height used to retrieve the <see cref="ProvenBlockHeader"/>.</param>
        /// <returns><see cref="ProvenBlockHeader"/> retrieved from the <see cref="ProvenBlockHeaderStore"/>.</returns>
        Task<ProvenBlockHeader> GetAsync(int blockHeight);

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/> items.
        /// </summary>
        /// <param name="fromBlockHeight">Block height to start querying from.</param>
        /// <param name="toBlockHeight">Block height to stop querying to.</param>
        /// <returns>A dictionary of <see cref="ProvenBlockHeader"/> items by block height key.</returns>
        Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight);

        /// <summary>
        /// Retrieves the <see cref="ProvenBlockHeader"/> of the current tip.
        /// </summary>
        /// <returns><see cref="ProvenBlockHeader"/></returns>
        Task<ProvenBlockHeader> GetTipAsync();

        /// <summary>
        /// Adds a <see cref="ProvenBlockHeader"/> to the internal concurrent dictionary. 
        /// </summary>
        /// <param name="provenBlockHeader">A <see cref="ProvenBlockHeader"/> item to add.</param>
        /// <param name="newTip">Block hash and height pair associated against the <see cref="ProvenBlockHeader"/> item.</param>
        void AddToPending(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip);       

        /// <summary> Retrieves the block height of the current <see cref="ProvenBlockHeader"/> tip.</summary>
        /// <returns>Block height of the current tip of the <see cref="ProvenBlockHeader"/>.</returns>
        Task<HashHeightPair> GetTipHashHeightAsync();
    }
}
