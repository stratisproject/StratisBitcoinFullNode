using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// This class determines which federated member to select as the next leader based on a change in block hieght.
    /// </summary>
    public interface ILeaderProvider
    {
        /// <summary>Public key of the current leader.</summary>
        PubKey CurrentLeaderKey { get; }

        void Update(BlockTipModel blockTipModel);
    }
}
