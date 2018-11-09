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
        private readonly IHttpClientFactory httpClientFactory;

        private readonly ILogger logger;

        private readonly int targetApiPort;

        protected RestSenderBase(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.targetApiPort = settings.CounterChainApiPort;
        }

        protected async Task SendAsync<T>(T model, string route)
        {
            var publicationUri = new Uri(
                $"http://localhost:{this.targetApiPort}/api/FederationGateway/{route}");

            using (var client = this.httpClientFactory.CreateClient())
            {
                var sendModel = (T)model;
                var request = new JsonContent(sendModel);

                try
                {
                    this.logger.LogDebug("Sending content {0} to Uri {1}", request, publicationUri);
                    HttpResponseMessage httpResponseMessage = await client.PostAsync(publicationUri, request);
                    this.logger.LogDebug("Response: {0}", httpResponseMessage);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to send {0}", model);
                }
            }
        }
    }
}
