using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlock
    {
        uint256 BlockHash { get; }

        int BlockHeight { get; }
    }
}