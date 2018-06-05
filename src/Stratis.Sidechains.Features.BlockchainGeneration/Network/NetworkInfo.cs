using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockchainGeneration
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


        private Target GetPowLimit()
        {
            if (this.NetworkName == SidechainNetwork.SidechainMainName) return new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            if (this.NetworkName == SidechainNetwork.SidechainTestName) return new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));
            if (this.NetworkName == SidechainNetwork.SidechainRegTestName) return new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            throw new ArgumentException("invalid sidechain network name");
        }

        internal static NetworkInfo FromNetworkInfoRequest(string networkName, NetworkInfoRequest request)
        {
            return new NetworkInfo(networkName, request.Time, request.Nonce, request.MessageStart, request.AddressPrefix, request.Port, request.RpcPort, request.ApiPort, request.CoinSymbol, request.GenesisHashHex);
        }

        private void ValidateOrAssignGenesisHash(string computedGenesisHashHex)
        {
            if (string.IsNullOrWhiteSpace(GenesisHashHex))
                GenesisHashHex = computedGenesisHashHex;
            else if (!GenesisHashHex.Equals(computedGenesisHashHex, StringComparison.InvariantCultureIgnoreCase))
                throw new InvalidOperationException("The supplied Genesis Hash is not in line with the computed Genesis Hash");
        }
    }
}