using Stratis.Bitcoin.NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface INetworkDifficulty
    {
        Target GetNetworkDifficulty();
    }
}
