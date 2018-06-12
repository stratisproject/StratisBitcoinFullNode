using System;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Network
{
   public class NetworkInfo : NetworkInfoRequest
    {
        private uint256 _genesisHash;
        [JsonIgnore]    //this is calculated
        public uint256 GenesisHash => _genesisHash ?? (_genesisHash = uint256.Parse(GenesisHashHex));

        public string NetworkName { get; }

        [JsonConstructor]
        public NetworkInfo(string networkName, uint time, uint nonce, uint messageStart, int addressPrefix, int port, int rpcPort, int apiPort, string coinSymbol, string genesisHashHex = null)
            : base(time, nonce, messageStart, addressPrefix, port, rpcPort, apiPort, coinSymbol, genesisHashHex)
        {
            this.NetworkName = networkName;
			//TODO: understand why the SidechainIdentifier.Instance needs to be created first
			//computing the hash here create a conflict
            //ComputeGenesisHash();
        }

        internal static NetworkInfo FromNetworkInfoRequest(string networkName, NetworkInfoRequest request)
        {
            return new NetworkInfo(networkName, request.Time, request.Nonce, request.MessageStart, request.AddressPrefix, request.Port, request.RpcPort, request.ApiPort, request.CoinSymbol, request.GenesisHashHex);
        }
    }
}