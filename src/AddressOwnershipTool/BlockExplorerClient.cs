using System;
using System.Net.Http;
using Newtonsoft.Json;

namespace AddressOwnershipTool
{
    public class BlockExplorerClient
    {
        private const string ExplorerBaseUrl = "https://stratisqbitninja2.azurewebsites.net/";

        public bool HasBalance(string address)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ExplorerBaseUrl);

                HttpResponseMessage response = client.GetAsync($"balances/{address}/summary").GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                dynamic balanceObject = JsonConvert.DeserializeObject<dynamic>(content);
                string balance = balanceObject.spendable.received.ToString();

                return balance != "0";
            }
        }
    }
}
