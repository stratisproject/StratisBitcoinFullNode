
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class RestMaturedBlocksRequesterTests
    {
        private IHttpClientFactory httpClientFactory;

        private HttpMessageHandler messageHandler;

        private HttpClient httpClient;

        private readonly ILoggerFactory loggerFactory;

        private readonly IFederationGatewaySettings federationSettings;

        private readonly ICrossChainTransferStore crossChainTransferStore;

        private readonly IMaturedBlockReceiver maturedBlocksReceiver;

        private readonly ILogger logger;

        public RestMaturedBlocksRequesterTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
            this.crossChainTransferStore = Substitute.For<ICrossChainTransferStore>();
            this.maturedBlocksReceiver = Substitute.For<IMaturedBlockReceiver>();
        }

        [Fact]
        public void StartShouldCallGetMatureDeposits()
        {
            this.crossChainTransferStore.NextMatureDepositHeight.Returns(1);

            this.httpClientFactory = Substitute.For<IHttpClientFactory>();

            bool called = false;
            this.httpClient = Substitute.For<HttpClient>();
            this.httpClient.PostAsync(Arg.Any<string>(), Arg.Any<HttpContent>())
              .ReturnsForAnyArgs(Task.Run(() =>
              {
                  called = true;
                  return new HttpResponseMessage();
              }));

            this.httpClientFactory.CreateClient(Arg.Any<string>()).Returns(this.httpClient);

            var restRequester = new RestMaturedBlockRequester(this.loggerFactory, this.federationSettings, this.httpClientFactory, this.crossChainTransferStore, this.maturedBlocksReceiver);
            restRequester.Start();

            // Wait one minute max.
            for (int i = 0; i < 600 && !called; i++)
                Thread.Sleep(100);

            Assert.True(called);
        }
    }
}
