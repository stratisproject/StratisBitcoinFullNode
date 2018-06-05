using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
    [Cmdlet(VerbsCommon.New, "SidechainUsingApi")]
    public class NewSidechainUsingApiCommand : NewSidechainCommandBase, IDisposable
    {
        [Parameter(Mandatory = false, Position = 7)]
        public string ApiUrl { get; set; }

        private HttpClient client;
        public class JsonContent : StringContent
        {
            public JsonContent(object obj) :
                base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json") {}
        }
        public NewSidechainUsingApiCommand()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        protected override void SaveSidechain(SidechainInfo sidechainInfo)
        {
            var uri = new Uri($"{ApiUrl ?? Constants.DefaultApiUrl}/sidechains/new-sidechain");
            var content = new JsonContent(sidechainInfo);
            var httpResponseMessage = this.client.PostAsync(uri, content).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to list chains from API, received error: {httpResponseMessage.StatusCode} - {httpResponseMessage.ReasonPhrase}");
        }

        #region IDisposable Support
        private int disposedValue = 0; // To detect redundant calls
        public void Dispose()
        {
            if (client == null || Interlocked.Increment(ref disposedValue) > 1) return;
            client.Dispose();
        }
        #endregion
    }
}