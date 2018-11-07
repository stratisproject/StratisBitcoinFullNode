using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// This class determines which federated member to select as the next leader based on a change in block hieght.
    /// </summary>
    public interface ILeaderProvider
    {
        NBitcoin.PubKey CurrentLeader { get; }

        void Update(BlockTipModel blockTipModel);
    }
}
