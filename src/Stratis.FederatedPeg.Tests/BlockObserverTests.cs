using System.Collections.Generic;

using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Xunit;
using BlockObserver = Stratis.FederatedPeg.Features.FederationGateway.Notifications.BlockObserver;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Microsoft.Extensions.Logging;

using Stratis.FederatedPeg.Tests.Utils;

namespace Stratis.FederatedPeg.Tests
{
    public class BlockObserverTests
    {
        private BlockObserver blockObserver;

        private readonly IFederationWalletSyncManager federationWalletSyncManager;

        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly IDepositExtractor depositExtractor;

        private readonly ILeaderProvider leaderProvider;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IFullNode fullNode;

        private readonly ConcurrentChain chain;

        private readonly uint minimumDepositConfirmations;

        private readonly IMaturedBlockSender maturedBlockSender;

        private readonly IBlockTipSender blockTipSender;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILoggerFactory loggerFactory;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        private readonly IReadOnlyList<IWithdrawal> extractedWithdrawals;

        public BlockObserverTests()
        {
            this.minimumDepositConfirmations = 10;

            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.federationGatewaySettings.MinimumDepositConfirmations.Returns(this.minimumDepositConfirmations);

            this.crossChainTransactionMonitor = Substitute.For<ICrossChainTransactionMonitor>();
            this.leaderProvider = Substitute.For<ILeaderProvider>();
            this.federationWalletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.fullNode = Substitute.For<IFullNode>();
            this.maturedBlockSender = Substitute.For<IMaturedBlockSender>();
            this.blockTipSender = Substitute.For<IBlockTipSender>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
            this.fullNode.NodeService<ConcurrentChain>().Returns(this.chain);
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();

            this.withdrawalExtractor = Substitute.For<IWithdrawalExtractor>();
            this.extractedWithdrawals = TestingValues.GetWithdrawals(2);
            this.withdrawalExtractor.ExtractWithdrawalsFromBlock(null, 0)
                .ReturnsForAnyArgs(this.extractedWithdrawals);

            this.withdrawalReceiver = Substitute.For<IWithdrawalReceiver>();

            this.depositExtractor = new DepositExtractor(
                this.loggerFactory,
                this.federationGatewaySettings,
                this.opReturnDataReader,
                this.fullNode);

            this.blockObserver = new BlockObserver(
                this.federationWalletSyncManager,
                this.crossChainTransactionMonitor,
                this.depositExtractor,
                this.withdrawalExtractor,
                this.withdrawalReceiver,
                this.maturedBlockSender,
                this.blockTipSender);
        }

        [Fact]
        public void BlockObserver_Should_Not_Try_To_Extract_Deposits_Before_MinimumDepositConfirmations()
        {
            var confirmations = (int)this.minimumDepositConfirmations - 1;

            var earlyBlock = new Block();
            var earlyChainHeaderBlock = new ChainedHeaderBlock(earlyBlock, new ChainedHeader(new BlockHeader(), uint256.Zero, confirmations));

            this.blockObserver.OnNext(earlyChainHeaderBlock);

            this.crossChainTransactionMonitor.Received(1).ProcessBlock(earlyBlock);
            this.federationWalletSyncManager.Received(1).ProcessBlock(earlyBlock);
            this.withdrawalExtractor.ReceivedWithAnyArgs(1).ExtractWithdrawalsFromBlock(earlyBlock, 0);
            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(this.extractedWithdrawals));
            this.blockTipSender.ReceivedWithAnyArgs(1).SendBlockTipAsync(null);
            this.maturedBlockSender.ReceivedWithAnyArgs(0).SendMaturedBlockDepositsAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Try_To_Extract_Deposits_After_MinimumDepositConfirmations()
        {
            var blockBuilder = this.ChainHeaderBlockBuilder();

            this.blockObserver.OnNext(blockBuilder.chainedHeaderBlock);

            this.crossChainTransactionMonitor.Received(1).ProcessBlock(blockBuilder.block);
            this.federationWalletSyncManager.Received(1).ProcessBlock(blockBuilder.block);
            this.maturedBlockSender.ReceivedWithAnyArgs(1).SendMaturedBlockDepositsAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Send_Block_Tip()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnNext(chainedHeaderBlock);

            this.blockTipSender.ReceivedWithAnyArgs(1).SendBlockTipAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Extract_Withdrawals()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnNext(chainedHeaderBlock);

            this.withdrawalExtractor.ReceivedWithAnyArgs(1).ExtractWithdrawalsFromBlock(null, 0);
        }

        [Fact]
        public void BlockObserver_Should_Send_Extracted_Withdrawals_To_WithdrawalReceiver()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnNext(chainedHeaderBlock);

            this.withdrawalReceiver.ReceivedWithAnyArgs(1).ReceiveWithdrawals(Arg.Is(this.extractedWithdrawals));
        }

        private (ChainedHeaderBlock chainedHeaderBlock, Block block) ChainHeaderBlockBuilder()
        {
            var confirmations = (int)this.minimumDepositConfirmations;

            var blockHeader = new BlockHeader();
            var chainedHeader = new ChainedHeader(blockHeader, uint256.Zero, confirmations);
            this.chain.GetBlock(0).Returns(chainedHeader);

            var block = new Block();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

            chainedHeaderBlock.ChainedHeader.Block = chainedHeaderBlock.Block;

            return (chainedHeaderBlock, block);
        }
    }
}
