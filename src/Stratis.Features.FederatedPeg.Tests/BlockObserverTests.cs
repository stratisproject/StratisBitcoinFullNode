using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Notifications;
using Stratis.Features.FederatedPeg.RestClients;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class BlockObserverTests
    {
        private readonly BlockObserver blockObserver;

        private readonly IFederationWalletSyncManager federationWalletSyncManager;

        private readonly IDepositExtractor depositExtractor;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IFullNode fullNode;

        private readonly ConcurrentChain chain;

        private readonly uint minimumDepositConfirmations;

        private readonly IFederationGatewayClient federationGatewayClient;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILoggerFactory loggerFactory;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        private readonly ISignals signals;

        private readonly IReadOnlyList<IWithdrawal> extractedWithdrawals;

        private readonly IConsensusManager consensusManager;

        public BlockObserverTests()
        {
            this.minimumDepositConfirmations = 10;

            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.federationGatewaySettings.MinimumDepositConfirmations.Returns(this.minimumDepositConfirmations);

            this.federationWalletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.fullNode = Substitute.For<IFullNode>();
            this.federationGatewayClient = Substitute.For<IFederationGatewayClient>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
            this.fullNode.NodeService<ConcurrentChain>().Returns(this.chain);
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();
            this.consensusManager = Substitute.For<IConsensusManager>();
            this.withdrawalExtractor = Substitute.For<IWithdrawalExtractor>();
            this.extractedWithdrawals = TestingValues.GetWithdrawals(2);
            this.withdrawalExtractor.ExtractWithdrawalsFromBlock(null, 0).ReturnsForAnyArgs(this.extractedWithdrawals);
  
            this.withdrawalReceiver = Substitute.For<IWithdrawalReceiver>();

            this.signals = Substitute.For<ISignals>();
            this.signals.OnBlockConnected.Returns(Substitute.For<EventNotifier<ChainedHeaderBlock>>());

            this.depositExtractor = new DepositExtractor(
                this.loggerFactory,
                this.federationGatewaySettings,
                this.opReturnDataReader,
                this.fullNode);

            this.blockObserver = new BlockObserver(
                this.federationWalletSyncManager,
                this.depositExtractor,
                this.withdrawalExtractor,
                this.withdrawalReceiver,
                this.federationGatewayClient,
                this.signals);
        }

        [Retry(3, Skip = TestingValues.SkipTests)]
        // This test requires retries because we are asserting result of a background thread that calls API.
        // This will work in real life but in tests it produces a race condition and therefore requires retries.
        public async Task BlockObserverShouldNotTryToExtractDepositsBeforeMinimumDepositConfirmationsAsync()
        {
            int confirmations = (int)this.minimumDepositConfirmations - 1;

            var earlyBlock = new Block();
            var earlyChainHeaderBlock = new ChainedHeaderBlock(earlyBlock, new ChainedHeader(new BlockHeader(), uint256.Zero, confirmations));

            this.blockObserver.OnBlockReceived(earlyChainHeaderBlock);

            this.federationWalletSyncManager.Received(1).ProcessBlock(earlyBlock);
            this.withdrawalExtractor.ReceivedWithAnyArgs(1).ExtractWithdrawalsFromBlock(earlyBlock, 0);
            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(this.extractedWithdrawals));

            await Task.Delay(500).ConfigureAwait(false);

            await this.federationGatewayClient.ReceivedWithAnyArgs(1).PushCurrentBlockTipAsync(null);
        }

        [Retry(3, Skip = TestingValues.SkipTests)]
        // This test requires retries because we are asserting result of a background thread that calls API.
        // This will work in real life but in tests it produces a race condition and therefore requires retries.
        public void BlockObserverShouldTryToExtractDepositsAfterMinimumDepositConfirmations()
        {
            (ChainedHeaderBlock chainedHeaderBlock, Block block) blockBuilder = this.ChainHeaderBlockBuilder();

            this.blockObserver.OnBlockReceived(blockBuilder.chainedHeaderBlock);

            this.federationWalletSyncManager.Received(1).ProcessBlock(blockBuilder.block);

            this.federationGatewayClient.ReceivedWithAnyArgs(1).PushCurrentBlockTipAsync(null);
        }

        [Retry(3, Skip = TestingValues.SkipTests)]
        // This test requires retries because we are asserting result of a background thread that calls API.
        // This will work in real life but in tests it produces a race condition and therefore requires retries.
        public async Task BlockObserverShouldSendBlockTipAsync()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnBlockReceived(chainedHeaderBlock);

            await Task.Delay(5000).ConfigureAwait(false);

            await this.federationGatewayClient.ReceivedWithAnyArgs(1).PushCurrentBlockTipAsync(null);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void BlockObserverShouldExtractWithdrawals()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnBlockReceived(chainedHeaderBlock);

            this.withdrawalExtractor.ReceivedWithAnyArgs(1).ExtractWithdrawalsFromBlock(null, 0);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void BlockObserverShouldSendExtractedWithdrawalsToWithdrawalReceiver()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnBlockReceived(chainedHeaderBlock);

            this.withdrawalReceiver.ReceivedWithAnyArgs(1).ReceiveWithdrawals(Arg.Is(this.extractedWithdrawals));
        }

        private (ChainedHeaderBlock chainedHeaderBlock, Block block) ChainHeaderBlockBuilder()
        {
            int confirmations = (int)this.minimumDepositConfirmations;

            var blockHeader = new BlockHeader();
            var chainedHeader = new ChainedHeader(blockHeader, uint256.Zero, confirmations);
            this.chain.GetBlock(0).Returns(chainedHeader);

            var block = new Block();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

            chainedHeaderBlock.ChainedHeader.Block = chainedHeaderBlock.Block;

            this.consensusManager.GetBlockDataAsync(uint256.Zero).Returns(chainedHeaderBlock);

            return (chainedHeaderBlock, block);
        }
    }
}
