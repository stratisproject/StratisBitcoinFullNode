using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Collateral;
using Stratis.Features.Collateral.CounterChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CollateralCheckerTests
    {
        private readonly ICollateralChecker collateralChecker;

        private readonly List<CollateralFederationMember> collateralFederationMembers;

        private readonly int collateralCheckHeight = 2000;

        public CollateralCheckerTests()
        {
            var loggerFactory = new LoggerFactory();
            IHttpClientFactory clientFactory = new Bitcoin.Controllers.HttpClientFactory();

            Network network = CirrusNetwork.NetworksSelector.Regtest();

            this.collateralFederationMembers = new List<CollateralFederationMember>()
            {
                new CollateralFederationMember(new PubKey("036317d97f911ce899fd0a360866d19f2dca5252c7960d4652d814ab155a8342de"), new Money(100), "addr1"),
                new CollateralFederationMember(new PubKey("02a08d72d47b3103261163c15aa2f6b0d007e1872ad9f5fddbfbd27bdb738156e9"), new Money(500), "addr2"),
                new CollateralFederationMember(new PubKey("03634c79d4e8e915cfb9f7bbef57bed32d715150836b7845b1a14c93670d816ab6"), new Money(100_000), "addr3")
            };

            List<IFederationMember> federationMembers = (network.Consensus.Options as PoAConsensusOptions).GenesisFederationMembers;
            federationMembers.Clear();
            federationMembers.AddRange(this.collateralFederationMembers);

            FederatedPegSettings fedPegSettings = FedPegTestsHelper.CreateSettings(network, out NodeSettings nodeSettings);

            CounterChainSettings settings = new CounterChainSettings(nodeSettings, Networks.Stratis.Regtest());
            var asyncMock = new Mock<IAsyncProvider>();
            asyncMock.Setup(a => a.RegisterTask(It.IsAny<string>(), It.IsAny<Task>()));

            ISignals signals = new Signals(loggerFactory, new DefaultSubscriptionErrorHandler(loggerFactory));
            IFederationManager fedManager = new CollateralFederationManager(nodeSettings, network, loggerFactory, new Mock<IKeyValueRepository>().Object, signals);

            fedManager.Initialize();

            this.collateralChecker = new CollateralChecker(loggerFactory, clientFactory, settings, fedManager, signals, network, asyncMock.Object, (new Mock<INodeLifetime>()).Object);
        }

        [Fact]
        public async Task InitializationTakesForeverIfCounterNodeIsOfflineAsync()
        {
            Task initTask = this.collateralChecker.InitializeAsync();

            await Task.Delay(10_000);

            // Task never finishes since counter chain node doesn't respond.
            Assert.False(initTask.IsCompleted);
        }

        [Fact]
        public async Task CanInitializeAndCheckCollateralAsync()
        {
            var blockStoreClientMock = new Mock<IBlockStoreClient>();

            var collateralData = new VerboseAddressBalancesResult(this.collateralCheckHeight + 1000)
            {
                BalancesData = new List<AddressIndexerData>()
                {
                    new AddressIndexerData()
                    {
                        Address = this.collateralFederationMembers[0].CollateralMainchainAddress,
                        BalanceChanges = new List<AddressBalanceChange>() { new AddressBalanceChange() { BalanceChangedHeight = 0, Deposited = true, Satoshi = this.collateralFederationMembers[0].CollateralAmount } }
                    },
                    new AddressIndexerData()
                    {
                        Address = this.collateralFederationMembers[1].CollateralMainchainAddress,
                        BalanceChanges = new List<AddressBalanceChange>() { new AddressBalanceChange() { BalanceChangedHeight = 0, Deposited = true, Satoshi = this.collateralFederationMembers[1].CollateralAmount + 10 } }
                    },
                    new AddressIndexerData()
                    {
                        Address = this.collateralFederationMembers[2].CollateralMainchainAddress,
                        BalanceChanges = new List<AddressBalanceChange>() { new AddressBalanceChange() { BalanceChangedHeight = 0, Deposited = true, Satoshi = this.collateralFederationMembers[2].CollateralAmount - 10 } }
                    }
                }
            };

            blockStoreClientMock.Setup(x => x.GetVerboseAddressesBalancesDataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(collateralData);

            this.collateralChecker.SetPrivateVariableValue("blockStoreClient", blockStoreClientMock.Object);

            await this.collateralChecker.InitializeAsync();

            Assert.True(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[0], this.collateralCheckHeight));
            Assert.True(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[1], this.collateralCheckHeight));
            Assert.False(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[2], this.collateralCheckHeight));

            // Now change what the client returns and make sure collateral check fails after update.
            AddressIndexerData updated = collateralData.BalancesData.First(b => b.Address == this.collateralFederationMembers[0].CollateralMainchainAddress);
            updated.BalanceChanges.First().Satoshi = this.collateralFederationMembers[0].CollateralAmount - 1;

            // Wait CollateralUpdateIntervalSeconds + 1 seconds

            await Task.Delay(21_000);
            Assert.False(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[0], this.collateralCheckHeight));

            this.collateralChecker.Dispose();
        }
    }
}
