namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public interface INetworkInfoRequest
    {
        int AddressPrefix { get; set; }
        int ApiPort { get; set; }
        string CoinSymbol { get; set; }
        string GenesisHashHex { get; }
        uint MessageStart { get; set; }
        uint Nonce { get; set; }
        int Port { get; set; }
        int RpcPort { get; set; }
        uint Time { get; set; }
    }
}