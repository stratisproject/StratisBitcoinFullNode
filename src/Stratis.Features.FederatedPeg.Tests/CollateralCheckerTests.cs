﻿using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CollateralCheckerTests
    {
        private readonly ICollateralChecker collateralChecker;

        private readonly List<CollateralFederationMember> collateralFederationMembers;

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

            FederationGatewaySettings settings = FedPegTestsHelper.CreateSettings(network, out NodeSettings nodeSettings);

            ISignals signals = new Signals(loggerFactory, new DefaultSubscriptionErrorHandler(loggerFactory));
            IFederationManager fedManager = new CollateralFederationManager(nodeSettings, network, loggerFactory, new Mock<IKeyValueRepository>().Object, signals);

            fedManager.Initialize();

            this.collateralChecker = new CollateralChecker(loggerFactory, clientFactory, settings, fedManager, signals);
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

            var collateralData = new Dictionary<string, Money>()
            {
                { this.collateralFederationMembers[0].CollateralMainchainAddress, this.collateralFederationMembers[0].CollateralAmount},
                { this.collateralFederationMembers[1].CollateralMainchainAddress, this.collateralFederationMembers[1].CollateralAmount + 10},
                { this.collateralFederationMembers[2].CollateralMainchainAddress, this.collateralFederationMembers[2].CollateralAmount - 10}
            };

            blockStoreClientMock.Setup(x => x.GetAddressBalancesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(collateralData);

            this.collateralChecker.SetPrivateVariableValue("blockStoreClient", blockStoreClientMock.Object);

            await this.collateralChecker.InitializeAsync();

            Assert.True(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[0]));
            Assert.True(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[1]));
            Assert.False(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[2]));

            // Now change what the client returns and make sure collateral check fails after update.
            collateralData[this.collateralFederationMembers[0].CollateralMainchainAddress] = this.collateralFederationMembers[0].CollateralAmount - 1;

            // Wait CollateralUpdateIntervalSeconds + 1 seconds

            await Task.Delay(21_000);
            Assert.False(this.collateralChecker.CheckCollateral(this.collateralFederationMembers[0]));

            this.collateralChecker.Dispose();
        }
    }
}
