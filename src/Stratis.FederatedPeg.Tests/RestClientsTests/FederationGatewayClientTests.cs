using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.RestClients;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests.RestClientsTests
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

        [Fact]
        public async Task SendBlockTip_Should_Be_Able_To_Send_IBlockTipAsync()
        {
            var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());

            await this.createClient().PushCurrentBlockTipAsync(blockTip).ConfigureAwait(false);

            this.logger.Received(0).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task SendBlockTip_Should_Log_Error_When_Failing_To_Send_IBlockTipAsync()
        {
            var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());

            HttpResponseMessage result = await this.createClient(true).PushCurrentBlockTipAsync(blockTip).ConfigureAwait(false);
            Assert.False(result.IsSuccessStatusCode);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.messageHandler?.Dispose();
            this.httpClient?.Dispose();
        }
    }
}
