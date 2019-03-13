using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class SignedMultisigTransactionBroadcasterTests : IDisposable
    {
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IDisposable leaderReceiverSubscription;
        private readonly ICrossChainTransferStore store;
        private readonly ILeaderReceiver leaderReceiver;
        private readonly IFederationGatewaySettings federationGatewaySettings;
        private readonly IBroadcasterManager broadcasterManager;
        private ISignedMultisigTransactionBroadcaster signedMultisigTransactionBroadcaster;
        private readonly ILeaderProvider leaderProvider;

        private readonly MempoolManager mempoolManager;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly MempoolSettings mempoolSettings;
        private readonly NodeSettings nodeSettings;
        private readonly BlockPolicyEstimator blockPolicyEstimator;
        private readonly TxMempool txMempool;
        private readonly IMempoolValidator mempoolValidator;
        private readonly IMempoolPersistence mempoolPersistence;
        private readonly ICoinView coinView;

        private const string PublicKey = "026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c";

        public SignedMultisigTransactionBroadcasterTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.leaderReceiver = Substitute.For<ILeaderReceiver>();
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.leaderReceiverSubscription = Substitute.For<IDisposable>();
            this.store = Substitute.For<ICrossChainTransferStore>();
            this.broadcasterManager = Substitute.For<IBroadcasterManager>();
            this.leaderProvider = Substitute.For<ILeaderProvider>();

            // Setup MempoolManager.
            this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
            this.nodeSettings = new NodeSettings(networksSelector: FederatedPegNetwork.NetworksSelector, protocolVersion: NBitcoin.Protocol.ProtocolVersion.ALT_PROTOCOL_VERSION);

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

        [Fact(Skip = TestingValues.SkipTests)]
        public async Task When_Not_The_CurrentLeader_Dont_Call_GetSignedTransactionsAsync()
        {
            this.federationGatewaySettings.PublicKey.Returns("dummykey");
            this.leaderProvider.CurrentLeaderKey.Returns(new PubKey(PublicKey));

            IObservable<ILeaderProvider> leaderStream = new[] { this.leaderProvider }.ToObservable();
            this.leaderReceiver.LeaderProvidersStream.Returns(leaderStream);

            this.signedMultisigTransactionBroadcaster = new SignedMultisigTransactionBroadcaster(
                this.loggerFactory,
                this.store,
                this.leaderReceiver,
                this.federationGatewaySettings,
                this.mempoolManager,
                this.broadcasterManager);

            await this.signedMultisigTransactionBroadcaster.BroadcastTransactionsAsync(this.leaderProvider).ConfigureAwait(false);

            this.store.DidNotReceive();
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public async Task When_CurrentLeader_Call_GetSignedTransactionsAsync()
        {
            this.federationGatewaySettings.PublicKey.Returns(PublicKey);
            this.leaderProvider.CurrentLeaderKey.Returns(new PubKey(PublicKey));

            IObservable<ILeaderProvider> leaderStream = new[] { this.leaderProvider }.ToObservable();
            this.leaderReceiver.LeaderProvidersStream.Returns(leaderStream);

            var emptyTransactionPair = new Dictionary<uint256, Transaction>();

            this.store.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).Returns(emptyTransactionPair);

            this.signedMultisigTransactionBroadcaster = new SignedMultisigTransactionBroadcaster(
                this.loggerFactory,
                this.store,
                this.leaderReceiver,
                this.federationGatewaySettings,
                this.mempoolManager,
                this.broadcasterManager);

            await this.signedMultisigTransactionBroadcaster.BroadcastTransactionsAsync(this.leaderProvider).ConfigureAwait(false);

            await this.store.Received().GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).ConfigureAwait(false);

            this.logger.Received().Log(LogLevel.Trace,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString() == "Signed multisig transactions do not exist in the CrossChainTransfer store."),
                null,
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public async Task When_CurrentLeader_BroadcastsTransactionAsync()
        {
            this.federationGatewaySettings.PublicKey.Returns(PublicKey);
            this.leaderProvider.CurrentLeaderKey.Returns(new PubKey(PublicKey));

            var transactionPair = new Dictionary<uint256, Transaction>
            {
                { new uint256(), new Transaction() }
            };

            this.store.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).Returns(transactionPair);

            IObservable<ILeaderProvider> leaderStream = new[] { this.leaderProvider }.ToObservable();
            this.leaderReceiver.LeaderProvidersStream.Returns(leaderStream);

            this.signedMultisigTransactionBroadcaster = new SignedMultisigTransactionBroadcaster(
                this.loggerFactory,
                this.store,
                this.leaderReceiver,
                this.federationGatewaySettings,
                this.mempoolManager,
                this.broadcasterManager);

            await this.signedMultisigTransactionBroadcaster.BroadcastTransactionsAsync(this.leaderProvider).ConfigureAwait(false);
            await this.store.Received().GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).ConfigureAwait(false);
            await this.broadcasterManager.Received().BroadcastTransactionAsync(Arg.Any<Transaction>());
        }

        public void Dispose()
        {
            this.leaderReceiverSubscription?.Dispose();
            this.leaderReceiver?.Dispose();
        }
    }
}
