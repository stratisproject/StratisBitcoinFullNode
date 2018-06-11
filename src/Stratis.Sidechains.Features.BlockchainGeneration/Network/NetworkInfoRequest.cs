using System;
using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Network
{
    public class NetworkInfoRequest : INetworkInfoRequest
    {
        public uint Time { get; set; }
        public uint Nonce { get; set; }
        public uint MessageStart { get; set; }
        public int AddressPrefix { get; set; }

        public int Port { get; set; }
        public int RpcPort { get; set; }
        public int ApiPort { get; set; }

        public string CoinSymbol { get; set; }

        public string GenesisHashHex { get; protected set; }

        [JsonIgnore]
        public DateTime DateTime
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(this.Time).DateTime; }
            set { this.Time = (uint) new DateTimeOffset(value).ToUnixTimeSeconds(); }
        }

        [JsonIgnore]
        public byte[] MessageStartAsBytes
        {
            get { return BitConverter.GetBytes(this.MessageStart); }
            set { this.MessageStart = BitConverter.ToUInt32(value, 0); }
        }

        public NetworkInfoRequest(uint time, uint nonce, uint messageStart, int addressPrefix, int port, int rpcPort,
            int apiPort, string coinSymbol, string genesisHashHex)
        {
            this.Time = time;
            this.Nonce = nonce;
            this.MessageStart = messageStart;
            this.AddressPrefix = addressPrefix;

            this.Port = port;
            this.RpcPort = rpcPort;
            this.ApiPort = apiPort;

            this.CoinSymbol = coinSymbol;

            this.GenesisHashHex = genesisHashHex;
        }
    }
}