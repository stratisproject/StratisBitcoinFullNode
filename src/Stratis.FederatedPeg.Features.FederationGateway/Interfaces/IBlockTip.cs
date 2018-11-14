using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IBlockTip
    {
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 Hash { get; }

        int Height { get; }
    }
}