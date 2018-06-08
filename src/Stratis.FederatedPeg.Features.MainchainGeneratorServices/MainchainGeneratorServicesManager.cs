using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.MainchainGeneratorServices.Models;

namespace Stratis.FederatedPeg.Features.MainchainGeneratorServices
{
    ///<inheritdoc/>
    public class MainchainGeneratorServicesManager : IMainchainGeneratorServicesManager
    {
        // Helper class used for returns in the API methods.
        public class JsonContent : StringContent
        {
            public JsonContent(object obj) :
                base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
            {

            }
        }

        // The mainchain network.
        private Network network;

        /// <summary>
        /// Create the manager with the mainchain network.
        /// </summary>
        /// <param name="network"></param>
        public MainchainGeneratorServicesManager(Network network)
        {
            //ToDo: is it a good idea to use a guard here or should it be a throw?
            Guard.Assert(network.ToChain() == Chain.Mainchain);
            this.network = network;
        }

        ///<inheritdoc/>
        public async Task InitSidechain(string sidechainName, int apiPortForSidechain, int multiSigM, int multiSigN, string folderFedMemberKey)
        {
            // Connect to the sidechain node.
            // Get the sidechain name to ensure we are communicating with the correct chain.
            string content = await this.GetSidechainName(apiPortForSidechain);
            string name = JsonConvert.DeserializeObject<string>(content);
            if (name != sidechainName) throw new ArgumentException($"A sidechain with name '{sidechainName}' was not found on port {apiPortForSidechain}.");

            // Load the fed members from the folder.
            // Checks N matches member count also.
            var memberFolderManager = new MemberFolderManager(folderFedMemberKey);
            var federation = memberFolderManager.LoadFederation(multiSigM, multiSigN);

            // Generate the redeem script and address files for mainchain.
            memberFolderManager.OutputScriptPubKeyAndAddress(federation, this.network);
            
            // Call into sidechain to generate the sidechain ScriptPubKey and Address.
            await this.OutputScriptPubKeyAndAddress(apiPortForSidechain, folderFedMemberKey, multiSigM, multiSigN);
        }

        //ToDo: Need to find a better way to handle the responses in these API client functions.
        // Client method to get the sidechain name.
        private async Task<string> GetSidechainName(int apiPortForSidechain)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/SidechainGeneratorServices/get-sidechainname");
                var httpResponseMessage = await client.GetAsync(uri);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return json;
            }
        }

        // Client method to generate the redeem and address on the sidechain.
        private async Task OutputScriptPubKeyAndAddress(int apiPortForSidechain, string folder, int multiSigM, int multiSigN)
        {
            var outputScriptPubKeyAndAddressRequest = new OutputScriptPubKeyAndAddressRequest()
            {
                FederationFolder = folder,
                MultiSigM = multiSigM,
                MultiSigN = multiSigN
            };
            
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/SidechainGeneratorServices/output-scriptpubkeyandaddress");
                var request = new JsonContent(outputScriptPubKeyAndAddressRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);
            }
        }
    }
}