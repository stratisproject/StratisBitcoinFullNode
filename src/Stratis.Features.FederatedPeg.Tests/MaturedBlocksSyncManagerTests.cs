using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.RestClients;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class MaturedBlocksSyncManagerTests
    {
        private readonly TestOnlyMaturedBlocksSyncManager syncManager;

        private readonly IFederationGatewayClient federationGatewayClient;
        private readonly ICrossChainTransferStore crossChainTransferStore;

        public MaturedBlocksSyncManagerTests()
        {
            ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
            this.federationGatewayClient = Substitute.For<IFederationGatewayClient>();
            this.crossChainTransferStore = Substitute.For<ICrossChainTransferStore>();

            this.syncManager = new TestOnlyMaturedBlocksSyncManager(this.crossChainTransferStore, this.federationGatewayClient, loggerFactory);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public async Task BlocksAreRequestedIfThereIsSomethingToRequestAsync()
        {
            this.crossChainTransferStore.NextMatureDepositHeight.Returns(5);
            this.crossChainTransferStore.RecordLatestMatureDepositsAsync(null).ReturnsForAnyArgs(true);

            var models = new List<MaturedBlockDepositsModel>() { new MaturedBlockDepositsModel(new MaturedBlockInfoModel(), new List<IDeposit>())};
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null).ReturnsForAnyArgs(Task.FromResult(models));

            bool delayRequired = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay shouldn't be required because not-empty list was provided.
            Assert.False(delayRequired);

            // Now provide empty list.
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null).ReturnsForAnyArgs(Task.FromResult(new List<MaturedBlockDepositsModel>() {}));

            bool delayRequired2 = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay is required because empty list was provided.
            Assert.True(delayRequired2);

            // Now provide null.
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null).ReturnsForAnyArgs(Task.FromResult(null as List<MaturedBlockDepositsModel>));

            bool delayRequired3 = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay is required because null list was provided.
            Assert.True(delayRequired3);
        }


        private class TestOnlyMaturedBlocksSyncManager : MaturedBlocksSyncManager
        {
            public TestOnlyMaturedBlocksSyncManager(ICrossChainTransferStore store, IFederationGatewayClient federationGatewayClient, ILoggerFactory loggerFactory)
                : base(store, federationGatewayClient, loggerFactory)
            {
            }

            public Task<bool> ExposedSyncBatchOfBlocksAsync()
            {
                return this.SyncBatchOfBlocksAsync();
            }
        }
    }
}
