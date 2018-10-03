using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Interface for database <see cref="ProvenBlockHeader"></see> repository.
    /// </summary>
    public interface IProvenBlockHeaderRepository : IProvenBlockHeaderProvider, IDisposable
    {
        /// <summary>
        /// Persists <see cref="ProvenBlockHeader"/> items to the database.
        /// </summary>
        /// <param name="provenBlockHeaders">List of <see cref="ProvenBlockHeader"/> items.</param>
        /// <param name="newTip">Block hash and height tip.</param>
        /// <returns><c>true</c> when a <see cref="ProvenBlockHeader"/> is saved to disk, otherwise <c>false</c>.</returns>
        Task<bool>PutAsync(List<ProvenBlockHeader> provenBlockHeaders, HashHeightPair newTip);
    }
}
