using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.RestClients;
using Stratis.Features.FederatedPeg.Tests.Utils;

namespace Stratis.Features.FederatedPeg.Tests.RestClientsTests
{
    // TODO log assertions in the tests? WYDT
    public class FederationGatewayClientTests : IDisposable
    {
        private IHttpClientFactory httpClientFactory;

        private HttpMessageHandler messageHandler;

        private HttpClient httpClient;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        public FederationGatewayClientTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
        }

        private FederationGatewayClient createClient(bool isFailingClient = false)
        {
            if (isFailingClient)
                TestingHttpClient.PrepareFailingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);
            else
                TestingHttpClient.PrepareWorkingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);

            IFederationGatewaySettings federationSettings = Substitute.For<IFederationGatewaySettings>();
            FederationGatewayClient client = new FederationGatewayClient(this.loggerFactory, federationSettings, this.httpClientFactory);

            return client;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.messageHandler?.Dispose();
            this.httpClient?.Dispose();
        }
    }
}
