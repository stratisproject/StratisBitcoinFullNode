using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IBlockTip
    {
        uint256 Hash { get; }

        int Height { get; }
    }
}