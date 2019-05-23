using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class PartialTransactionsRequesterTests
    {
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly ICrossChainTransferStore store;
        private readonly IFederatedPegSettings federatedPegSettings;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly IConnectionManager connectionManager;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly IFederationWalletManager federationWalletManager;

        public PartialTransactionsRequesterTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.federatedPegSettings = Substitute.For<IFederatedPegSettings>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.store = Substitute.For<ICrossChainTransferStore>();
            this.asyncProvider = Substitute.For<IAsyncProvider>();
            this.nodeLifetime = Substitute.For<INodeLifetime>();

            this.ibdState = Substitute.For<IInitialBlockDownloadState>();
            this.federationWalletManager = Substitute.For<IFederationWalletManager>();
            this.federationWalletManager.IsFederationWalletActive().Returns(true);
        }


        [Fact]
        public async Task DoesntBroadcastInIBD()
        {
            this.ibdState.IsInitialBlockDownload().Returns(true);

            var partialRequester = new PartialTransactionRequester(
                this.loggerFactory,
                this.store,
                this.asyncProvider,
                this.nodeLifetime,
                this.connectionManager,
                this.federatedPegSettings,
                this.ibdState,
                this.federationWalletManager);

            await partialRequester.BroadcastPartialTransactionsAsync();

            this.store.Received(0).GetTransfersByStatus(Arg.Any<CrossChainTransferStatus[]>());
        }

        [Fact]
        public async Task DoesntBroadcastWithInactiveFederation()
        {
            this.federationWalletManager.IsFederationWalletActive().Returns(false);

            var partialRequester = new PartialTransactionRequester(
                this.loggerFactory,
                this.store,
                this.asyncProvider,
                this.nodeLifetime,
                this.connectionManager,
                this.federatedPegSettings,
                this.ibdState,
                this.federationWalletManager);

            await partialRequester.BroadcastPartialTransactionsAsync();

            this.store.Received(0).GetTransfersByStatus(Arg.Any<CrossChainTransferStatus[]>());
        }
    }
}
