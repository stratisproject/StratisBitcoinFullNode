using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Controllers;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// RPC method to bridge into api calls
    /// </summary>
    public class ApiRPCController : FeatureController
    {
        private ApiSettings apiSettings;

        public ApiRPCController(ApiSettings apiSettings, IFullNode fullNode) : base(fullNode: fullNode)
        {
            this.apiSettings = apiSettings;
        }

        /// <summary>
        /// Forward API request from RPC to API.
        /// </summary>
        /// <param name="verb">GET, POST, PUT, DELETE</param>
        /// <param name="command">Command endpoint.</param>
        /// <param name="request">if GET/DELETE then url parameters (if any), if POST/PUT then Json request encoded as Base64.</param>
        /// <example>CallApiAsync("POST", "wallet/load", "e3BhcmFtOjEsIHBhcmFtOjJ9");</example>
        /// <example>CallApiAsync("GET", "wallet/general-info", "?name=default&param2=3"</example>
        /// <returns>Json response.</returns>
        [ActionName("callapi")]
        [ActionDescription("Forwards request to swagger api for processing.")]
        public async Task<string> CallApiAsync(string verb, string command, string request)
        {
            string response = null;

            // Process API call.
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var url = $"{this.apiSettings.ApiUri.AbsoluteUri}api/{command}";

                if (verb?.Equals("GET", StringComparison.InvariantCultureIgnoreCase) ?? true)
                {
                    url += request;
                    response = await client.GetStringAsync(url);
                }
                else if (verb.Equals("DELETE", StringComparison.InvariantCultureIgnoreCase))
                {
                    url += request;
                    HttpResponseMessage deleteResponse = await client.DeleteAsync(url);
                    response = deleteResponse.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    // Convert request from base64 to JSON to pass to Api.
                    var requestJson = Encoding.UTF8.GetString(Convert.FromBase64String(request));
                    StringContent content = new StringContent(requestJson, UTF8Encoding.UTF8, "application/json");
                    if (verb.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
                    {
                        HttpResponseMessage postResponse = await client.PostAsync(url, content);
                        response = postResponse.Content.ReadAsStringAsync().Result;
                    }
                    else if (verb.Equals("PUT", StringComparison.InvariantCultureIgnoreCase))
                    {
                        HttpResponseMessage postResponse = await client.PutAsync(url, content);
                        response = postResponse.Content.ReadAsStringAsync().Result;
                    }
                }

                return response;
            }
        }
    }
}
