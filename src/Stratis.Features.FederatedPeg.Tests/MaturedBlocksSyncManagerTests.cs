using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Controllers;
using Stratis.Features.FederatedPeg.Controllers;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.TargetChain;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class MaturedBlocksSyncManagerTests
    {
        private readonly TestOnlyMaturedBlocksSyncManager syncManager;

        private readonly IFederationGatewayClient federationGatewayClient;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IAsyncProvider asyncProvider;

        public MaturedBlocksSyncManagerTests()
        {
            ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
            this.federationGatewayClient = Substitute.For<IFederationGatewayClient>();
            this.crossChainTransferStore = Substitute.For<ICrossChainTransferStore>();
            this.asyncProvider = Substitute.For<IAsyncProvider>();

            this.syncManager = new TestOnlyMaturedBlocksSyncManager(this.crossChainTransferStore, this.federationGatewayClient, loggerFactory, this.asyncProvider);
        }

        [Fact]
        public async Task BlocksAreRequestedIfThereIsSomethingToRequestAsync()
        {
            this.crossChainTransferStore.NextMatureDepositHeight.Returns(5);
            this.crossChainTransferStore.RecordLatestMatureDepositsAsync(null).ReturnsForAnyArgs(new RecordLatestMatureDepositsResult().Succeeded());

            var models = new List<MaturedBlockDepositsModel>() { new MaturedBlockDepositsModel(new MaturedBlockInfoModel(), new List<IDeposit>()) };
            var apiResult = ApiResult<List<MaturedBlockDepositsModel>>.Ok(models);
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null, new CancellationToken()).ReturnsForAnyArgs(Task.FromResult(apiResult));

            bool delayRequired = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay shouldn't be required because not-empty list was provided.
            Assert.False(delayRequired);

            // Now provide empty list.
            apiResult = ApiResult<List<MaturedBlockDepositsModel>>.Ok(new List<MaturedBlockDepositsModel>() { });
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null, new CancellationToken()).ReturnsForAnyArgs(Task.FromResult(apiResult));

            bool delayRequired2 = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay is required because empty list was provided.
            Assert.True(delayRequired2);

            // Now provide null.
            apiResult = ApiResult<List<MaturedBlockDepositsModel>>.Ok(null as List<MaturedBlockDepositsModel>);
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null, new CancellationToken()).ReturnsForAnyArgs(Task.FromResult(apiResult));

            bool delayRequired3 = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay is required because null list was provided.
            Assert.True(delayRequired3);
        }

        private class TestOnlyMaturedBlocksSyncManager : MaturedBlocksSyncManager
        {
            public TestOnlyMaturedBlocksSyncManager(ICrossChainTransferStore store, IFederationGatewayClient federationGatewayClient, ILoggerFactory loggerFactory, IAsyncProvider asyncProvider)
                : base(store, federationGatewayClient, loggerFactory, asyncProvider)
            {
            }

            public Task<bool> ExposedSyncBatchOfBlocksAsync()
            {
                return this.SyncBatchOfBlocksAsync(new CancellationToken());
            }
        }
    }
}
