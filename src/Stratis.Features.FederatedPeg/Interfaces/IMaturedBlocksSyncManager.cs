using System;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Handles block syncing between gateways on 2 chains. This node will request
    /// blocks from another chain to look for cross chain deposit transactions.
    /// </summary>
    public interface IMaturedBlocksSyncManager : IDisposable
    {
        /// <summary>Starts requesting blocks from another chain.</summary>
        void Initialize();
    }
}
