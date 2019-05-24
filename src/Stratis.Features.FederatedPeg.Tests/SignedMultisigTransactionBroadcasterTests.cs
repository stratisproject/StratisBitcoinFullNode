using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Events;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class SignedMultisigTransactionBroadcasterTests : IDisposable
    {
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IDisposable leaderReceiverSubscription;
        private readonly IFederatedPegSettings federatedPegSettings;
        private readonly IBroadcasterManager broadcasterManager;

        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly MempoolManager mempoolManager;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly MempoolSettings mempoolSettings;
        private readonly NodeSettings nodeSettings;
        private readonly BlockPolicyEstimator blockPolicyEstimator;
        private readonly TxMempool txMempool;
        private readonly IMempoolValidator mempoolValidator;
        private readonly IMempoolPersistence mempoolPersistence;
        private readonly ICoinView coinView;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly ISignals signals;

        private const string PublicKey = "026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c";

        public SignedMultisigTransactionBroadcasterTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.federatedPegSettings = Substitute.For<IFederatedPegSettings>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.leaderReceiverSubscription = Substitute.For<IDisposable>();
            this.broadcasterManager = Substitute.For<IBroadcasterManager>();
            this.asyncProvider = Substitute.For<IAsyncProvider>();
            this.nodeLifetime = Substitute.For<INodeLifetime>();

            this.ibdState = Substitute.For<IInitialBlockDownloadState>();
            this.federationWalletManager = Substitute.For<IFederationWalletManager>();
            this.federationWalletManager.IsFederationWalletActive().Returns(true);
            this.signals = new Signals(this.loggerFactory, null);

            // Setup MempoolManager.
            this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
            this.nodeSettings = new NodeSettings(networksSelector: CirrusNetwork.NetworksSelector, protocolVersion: NBitcoin.Protocol.ProtocolVersion.ALT_PROTOCOL_VERSION);

            this.mempoolSettings = new MempoolSettings(this.nodeSettings)
            {
                MempoolExpiry = MempoolValidator.DefaultMempoolExpiry
            };

            this.blockPolicyEstimator = new BlockPolicyEstimator(
                this.mempoolSettings,
                this.loggerFactory,
                this.nodeSettings);

            this.txMempool = new TxMempool(
                this.dateTimeProvider,
                this.blockPolicyEstimator,
                this.loggerFactory,
                this.nodeSettings);

            this.mempoolValidator = Substitute.For<IMempoolValidator>();
            this.mempoolPersistence = Substitute.For<IMempoolPersistence>();
            this.coinView = Substitute.For<ICoinView>();

            this.mempoolManager = new MempoolManager(
                new MempoolSchedulerLock(),
                this.txMempool,
                this.mempoolValidator,
                this.dateTimeProvider,
                this.mempoolSettings,
                this.mempoolPersistence,
                this.coinView,
                this.loggerFactory,
                this.nodeSettings.Network);
        }

        [Fact]
        public async Task Call_GetSignedTransactionsAsync_Signed_Transactions_Broadcasts()
        {
            this.federatedPegSettings.PublicKey.Returns(PublicKey);

            using (var signedMultisigTransactionBroadcaster = new SignedMultisigTransactionBroadcaster(
               this.loggerFactory,
               this.mempoolManager,
               this.broadcasterManager,
               this.ibdState,
               this.federationWalletManager,
               this.signals))
            {
                signedMultisigTransactionBroadcaster.Start();

                var partial = new Transaction();
                var xfer = new CrossChainTransfer();
                xfer.SetPartialTransaction(partial);

                this.signals.Publish(new CrossChainTransferTransactionFullySigned(xfer));
                await Task.Delay(100); //the event subscriber handles the event asynchronously so let's wait a bit to give it the time to complete.

                await this.broadcasterManager.Received(1).BroadcastTransactionAsync(Arg.Any<Transaction>());
            }
        }

        [Fact]
        public async Task Dont_Do_Work_In_IBD()
        {
            this.ibdState.IsInitialBlockDownload().Returns(true);

            using (var signedMultisigTransactionBroadcaster = new SignedMultisigTransactionBroadcaster(
               this.loggerFactory,
               this.mempoolManager,
               this.broadcasterManager,
               this.ibdState,
               this.federationWalletManager,
               this.signals))
            {
                signedMultisigTransactionBroadcaster.Start();

                var partial = new Transaction();
                var xfer = new CrossChainTransfer();
                xfer.SetPartialTransaction(partial);

                this.signals.Publish(new CrossChainTransferTransactionFullySigned(xfer));
                await Task.Delay(100); //the event subscriber handles the event asynchronously so let's wait a bit to give it the time to complete.

                await this.broadcasterManager.Received(0).BroadcastTransactionAsync(Arg.Any<Transaction>());
            }
        }

        [Fact]
        public async Task Dont_Do_Work_Inactive_Federation()
        {
            this.federationWalletManager.IsFederationWalletActive().Returns(false);

            this.ibdState.IsInitialBlockDownload().Returns(true);

            using (var signedMultisigTransactionBroadcaster = new SignedMultisigTransactionBroadcaster(
               this.loggerFactory,
               this.mempoolManager,
               this.broadcasterManager,
               this.ibdState,
               this.federationWalletManager,
               this.signals))
            {
                signedMultisigTransactionBroadcaster.Start();

                var partial = new Transaction();
                var xfer = new CrossChainTransfer();
                xfer.SetPartialTransaction(partial);

                this.signals.Publish(new CrossChainTransferTransactionFullySigned(xfer));
                await Task.Delay(100); //the event subscriber handles the event asynchronously so let's wait a bit to give it the time to complete.

                await this.broadcasterManager.Received(0).BroadcastTransactionAsync(Arg.Any<Transaction>());
            }
        }

        public void Dispose()
        {
            this.leaderReceiverSubscription?.Dispose();
        }
    }
}
