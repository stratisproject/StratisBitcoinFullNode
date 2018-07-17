using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Stratis.Bitcoin.Apps.Browser.Dto;
using Stratis.Bitcoin.Apps.Browser.Interfaces;

namespace Stratis.Bitcoin.Apps.Browser.Services
{
    public class AppsService : IAppsService
    {
        private readonly HttpClient httpClient;

        public AppsService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<List<StratisApp>> GetApplicationsAsync()
        {
            var serializer = new DataContractJsonSerializer(typeof(List<StratisApp>));
            Task<System.IO.Stream> streamTask = this.httpClient.GetStreamAsync("http://localhost:38221/api/apps/all");
            return serializer.ReadObject(await streamTask) as List<StratisApp>;
        }
    }
}
