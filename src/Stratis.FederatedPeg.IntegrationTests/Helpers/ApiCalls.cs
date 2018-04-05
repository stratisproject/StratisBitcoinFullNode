using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.MainchainGeneratorServices.Models;
using Stratis.FederatedPeg.Features.MainchainRuntime.Models;

namespace Stratis.FederatedPeg.IntegrationTests
{
    internal static class ApiCalls
    {
        public class JsonContent : StringContent
        {
            public JsonContent(object obj) :
                base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
            {

            }
        }

        public static async Task InitSidechain(string sidechainName, int apiPortForMainchain, int apiPortForSidechain, int multiSigN, int multiSigM, string folderFedMemberKey)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var initSidechainRequest = new InitSidechainRequest();
                initSidechainRequest.SidechainName = sidechainName;
                initSidechainRequest.ApiPortForSidechain = apiPortForSidechain;
                initSidechainRequest.MultiSigN = multiSigN;
                initSidechainRequest.MultiSigM = multiSigM;
                initSidechainRequest.FolderFedMemberKeys = folderFedMemberKey;

                var uri = new Uri($"http://localhost:{apiPortForMainchain}/api/MainchainGeneratorServices/init-sidechain");
                var request = new JsonContent(initSidechainRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);

                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    string content = await httpResponseMessage.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ErrorResponse>(content);
                    string message = result.Errors[0].Message;
                    throw new Exception(message);
                }
            }
        }

        public static async Task<string> Mnemonic(int apiPortForSidechain)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/Wallet/mnemonic");
                var httpResponseMessage = await client.GetAsync(uri);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<string>(json);
            }
        }

        public static async Task<string> UnusedAddress(int apiPortForSidechain, string walletName)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/Wallet/unusedaddress?WalletName={walletName}&AccountName=account%200");
                var httpResponseMessage = await client.GetAsync(uri);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<string>(json);
            }
        }

        public static async Task CreateGeneralPurposeWallet(int apiPortForSidechain, string walletName,
            string password)
        {
            var walletCreationRequest = new Stratis.Bitcoin.Features.GeneralPurposeWallet.Models.WalletCreationRequest()
            {
                Name = walletName,
                Password = password
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/generalpurposewallet/create");
                var request = new JsonContent(walletCreationRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return;// JsonConvert.DeserializeObject<string>(json);
            }
        }

        public static async Task<string> Create(int apiPortForSidechain, string mnemonic, string walletName,
            string folderPath)
        {
            var walletCreationRequest = new Stratis.Bitcoin.Features.Wallet.Models.WalletCreationRequest();
            walletCreationRequest.Network = "SidechainRegTest";
            walletCreationRequest.Mnemonic = mnemonic;
            walletCreationRequest.Name = walletName;
            walletCreationRequest.Password = "1234";
            walletCreationRequest.FolderPath = folderPath;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/Wallet/create");
                var request = new JsonContent(walletCreationRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<string>(json);
            }
        }

        public static async Task<WalletBuildTransactionModel> BuildTransaction(int apiPortForSidechain,
            SendFundsToSidechainRequest sendFundsToSidechainRequest)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/MainchainRuntime/build-transaction");
                var request = new JsonContent(sendFundsToSidechainRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WalletBuildTransactionModel>(json, new UInt256JsonConverter());
            }
        }

        public static async Task<WalletBuildTransactionModel> SendTransaction(int apiPortForSidechain,
            SendTransactionRequest sendTransactionRequest)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/MainchainRuntime/send-transaction");
                var request = new JsonContent(sendTransactionRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WalletBuildTransactionModel>(json);
            }
        }
    }
}
