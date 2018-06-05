using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    internal class SidechainsManager : ISidechainsManager
    {
        private JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Include,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };
        private CoinDetails _coinDetails;
        private readonly string _folder;
        private readonly string _networkName;
        private readonly string _sidechainName;

        public SidechainsManager(NodeSettings nodeSettings)
        {
            //TODO: probably add the infornations about the sidechains in the nodeSettings 
            var directoryInfo = new DirectoryInfo(nodeSettings.DataDir);
            _folder = directoryInfo.Parent.Parent.FullName;
            _networkName = nodeSettings.Network.Name;
            _sidechainName = directoryInfo.Parent.Name;
        }

        public async Task<Dictionary<string, SidechainInfo>> ListSidechains()
        {
            return await this.GetSidechains();
        }

        public async Task<CoinDetails> GetCoinDetails()
        {
            if (_coinDetails != null) return _coinDetails;
            var infoProvider = new DefaultSidechainInfoProvider(_folder);
            var sidechainInfo = await infoProvider.GetSidechainInfoAsync(_sidechainName);
            //if(sidechainInfo.NetworkInfoByName[])
            var networkInfo = sidechainInfo.NetworkInfoByName[_networkName];
            _coinDetails = new CoinDetails(networkInfo.CoinSymbol, sidechainInfo.CoinName, sidechainInfo.CoinType);
            return _coinDetails;

        }

        public async Task NewSidechain(SidechainInfoRequest sidechainInfoRequest)
        {
            var dictionaryOut = await this.GetSidechains();
            if (dictionaryOut.Keys.Contains(sidechainInfoRequest.ChainName))
                throw new ArgumentException($"A sidechain with the name ${sidechainInfoRequest.ChainName} already exists.");

            var sidechainInfo = new SidechainInfo(sidechainInfoRequest.ChainName, 
                sidechainInfoRequest.CoinName, 
                sidechainInfoRequest.CoinType,
                sidechainInfoRequest.MainNet, 
                sidechainInfoRequest.TestNet, 
                sidechainInfoRequest.RegTest);
            dictionaryOut.Add(sidechainInfo.ChainName, sidechainInfo);
            await this.SaveSidechains(dictionaryOut);
        }

        private async Task SaveSidechains(Dictionary<string, SidechainInfo> dictionary)
        {
            string filename = Path.Combine(_folder, "sidechains.json");

            string json = JsonConvert.SerializeObject(dictionary, Formatting.Indented, this.jsonSerializerSettings);
            using (var fileStream = File.OpenWrite(filename))
            {
                var bytesToWrite = Encoding.UTF8.GetBytes(json);
                await fileStream.WriteAsync(bytesToWrite, 0, bytesToWrite.Length);
            }
        }

        private async Task<Dictionary<string, SidechainInfo>> GetSidechains()
        {
            string filename = Path.Combine(_folder, "sidechains.json");
            if (System.IO.File.Exists(filename) == false)
                return new Dictionary<string, SidechainInfo>();
            else
            {
                using (var fileStream = File.OpenText(filename))
                {
                    var content = await fileStream.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(content);
                }
            }
        }
    }
}
