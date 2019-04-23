using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CollateralCheckerTests
    {
        private readonly CollateralChecker collateralChecker;

        public CollateralCheckerTests()
        {
            var loggerFactory = new LoggerFactory();
            IHttpClientFactory clientFactory = new Bitcoin.Controllers.HttpClientFactory();

            Network network = FederatedPegNetwork.NetworksSelector.Regtest();
            FederationGatewaySettings settings = FedPegTestsHelper.CreateSettings(network, out NodeSettings nodeSettings);

            ISignals signals = new Signals(loggerFactory, new DefaultSubscriptionErrorHandler(loggerFactory));
            IFederationManager fedManager = new CollateralFederationManager(nodeSettings, network, loggerFactory, new Mock<IKeyValueRepository>().Object, signals);

            fedManager.Initialize();

            this.collateralChecker = new CollateralChecker(loggerFactory, clientFactory, settings, fedManager, signals);
        }

        [Fact]
        public async Task TestSmthAsync()
        {
            await this.collateralChecker.InitializeAsync();

            this.collateralChecker.Dispose();
        }

        // TODO tests for the updating and checking of the collateral requirement, and possibly the exceptions in CollateralFederationManager.Initialize
    }
}
