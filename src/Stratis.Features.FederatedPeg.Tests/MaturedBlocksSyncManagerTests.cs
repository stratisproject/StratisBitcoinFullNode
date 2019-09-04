using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.AsyncWork;
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
            var result = SerializableResult<List<MaturedBlockDepositsModel>>.Ok(models);
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null).ReturnsForAnyArgs(Task.FromResult(result));

            bool delayRequired = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay shouldn't be required because a non-empty list was provided.
            Assert.False(delayRequired);

            // Now provide empty list.
            result = SerializableResult<List<MaturedBlockDepositsModel>>.Ok(new List<MaturedBlockDepositsModel>() { });
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null).ReturnsForAnyArgs(Task.FromResult(result));

            bool delayRequired2 = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay is required because an empty list was provided.
            Assert.True(delayRequired2);

            // Now provide null.
            result = SerializableResult<List<MaturedBlockDepositsModel>>.Ok(null as List<MaturedBlockDepositsModel>);
            this.federationGatewayClient.GetMaturedBlockDepositsAsync(null).ReturnsForAnyArgs(Task.FromResult(result));

            bool delayRequired3 = await this.syncManager.ExposedSyncBatchOfBlocksAsync();
            // Delay is required because a null list was provided.
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
                return this.SyncBatchOfBlocksAsync();
            }
        }
    }
}
