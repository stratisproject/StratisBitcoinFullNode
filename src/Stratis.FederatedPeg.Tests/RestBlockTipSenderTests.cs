using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class RestBlockTipSenderTests : IDisposable
    {
        private IHttpClientFactory httpClientFactory;

        private HttpMessageHandler messageHandler;

        private HttpClient httpClient;

        private ILoggerFactory loggerFactory;

        private IFederationGatewaySettings federationSettings;

        private ILogger logger;

        public RestBlockTipSenderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
        }

        [Fact]
        public async Task SendBlockTip_Should_Be_Able_To_Send_IBlockTipAsync()
        {
            TestingHttpClient.PrepareWorkingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);

            var restSender = new RestBlockTipSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);

            var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt());

            await restSender.SendBlockTipAsync(blockTip).ConfigureAwait(false);

            this.logger.Received(0).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task SendBlockTip_Should_Log_Error_When_Failing_To_Send_IBlockTipAsync()
        {
            TestingHttpClient.PrepareFailingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);

            var restSender = new RestBlockTipSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);

            var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt());

            await restSender.SendBlockTipAsync(blockTip).ConfigureAwait(false);

            this.logger.Received(1).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Is<Exception>(e => e != null), Arg.Any<Func<object, Exception, string>>());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.messageHandler?.Dispose();
            this.httpClient?.Dispose();
        }
    }
}
