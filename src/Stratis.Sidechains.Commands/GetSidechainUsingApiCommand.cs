using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Newtonsoft.Json;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Commands
{
    /// <summary>
    /// <para type="synopsis">This is the cmdlet synopsis.</para>
    /// <para type="description">This is part of the longer cmdlet description.</para>
    /// <para type="description">Also part of the longer cmdlet description.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "SidechainsUsingApi")]
    public class GetSidechainsUsingApiCommand : GetSidechainsCommandBase, IDisposable
    {
        private HttpClient client;

        [Parameter(Mandatory = false, Position = 1)]
        public string ApiUrl { get; set; }

        public GetSidechainsUsingApiCommand()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        protected override Dictionary<string, SidechainInfo> GetSidechains()
        {
            var uri = new Uri($"{ApiUrl ?? Constants.DefaultApiUrl}/sidechains/list-sidechains");
            var httpResponseMessage = client.GetAsync(uri).Result;
            if (!httpResponseMessage.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to list chains from API, received error: {httpResponseMessage.StatusCode} - {httpResponseMessage.ReasonPhrase}");

            var sidechains = JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(httpResponseMessage.Content.ReadAsStringAsync().Result);
            return sidechains;
        }

        #region IDisposable Support
        private int disposedValue = 0; // To detect redundant calls
        public void Dispose()
        {
            if (client == null  ||  Interlocked.Increment(ref disposedValue) > 1) return;
            client.Dispose();
        }
        #endregion


    }
}