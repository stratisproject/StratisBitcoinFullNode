using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.RestClients;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.RestClientsTests
{
    public class RestApiClientBaseTests
    {
        private IHttpClientFactory httpClientFactory;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        public RestApiClientBaseTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.httpClientFactory = new HttpClientFactory();
        }

        [Fact]
        public async Task TestRetriesCountAsync()
        {
            IFederationGatewaySettings federationSettings = Substitute.For<IFederationGatewaySettings>();

            var testClient = new TestRestApiClient(this.loggerFactory, federationSettings, this.httpClientFactory);

            HttpResponseMessage result = await testClient.CallThatWillAlwaysFail().ConfigureAwait(false);

            Assert.Equal(RestApiClientBase.RetryCount, testClient.RetriesCount);
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }
    }

    public class TestRestApiClient : RestApiClientBase
    {
        public int RetriesCount { get; private set; }

        public TestRestApiClient(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
            this.RetriesCount = 0;
        }

        public Task<HttpResponseMessage> CallThatWillAlwaysFail()
        {
            return this.SendPostRequestAsync("stringModel", "nonExistentAPIMethod", CancellationToken.None);
        }

        protected override void OnRetry(Exception exception, TimeSpan delay)
        {
            this.RetriesCount++;
            base.OnRetry(exception, delay);
        }
    }
}
