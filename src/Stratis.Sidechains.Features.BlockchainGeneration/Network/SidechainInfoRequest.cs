namespace Stratis.Sidechains.Features.BlockchainGeneration.Network
{
    public class SidechainInfoRequest : SidechainInfoBase<NetworkInfoRequest>
    {
        public SidechainInfoRequest(string chainName, string coinName, int coinType,
            NetworkInfoRequest mainNet, NetworkInfoRequest testNet, NetworkInfoRequest regTest)
            : base(chainName, coinName, coinType, mainNet, testNet, regTest)
        { }
    }
}