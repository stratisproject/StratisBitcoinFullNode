using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public abstract class RestSenderBase
    {
        private readonly ILogger logger;

        private readonly int targetApiPort;

        public RestSenderBase(ILoggerFactory loggerFactory, IFederationGatewaySettings settings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.targetApiPort = settings.CounterChainApiPort;
        }

        protected async Task SendAsync<T>(T model, string route)
        {
            var publicationUri = new Uri(
                $"http://localhost:{this.targetApiPort}/api/FederationGateway/{route}");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var sendModel = (T)model;
                var request = new JsonContent(sendModel);

                try
                {
                    HttpResponseMessage httpResponseMessage = await client.PostAsync(publicationUri, request);
                    this.logger.LogDebug("Response: {0}", await httpResponseMessage.Content.ReadAsStringAsync());
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to send {0}", model);
                }
            }
        }
    }
}
