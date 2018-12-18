using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Notifications;
using Stratis.FederatedPeg.Features.FederationGateway.RestClients;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
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

        private readonly IMaturedBlocksProvider maturedBlocksProvider;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILoggerFactory loggerFactory;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

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
            this.maturedBlocksProvider = Substitute.For<IMaturedBlocksProvider>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
            this.fullNode.NodeService<ConcurrentChain>().Returns(this.chain);
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();
            this.consensusManager = Substitute.For<IConsensusManager>();
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

            this.maturedBlocksProvider = new MaturedBlocksProvider(
                this.loggerFactory,
                this.chain,
                this.depositExtractor,
                Substitute.For<IBlockRepository>(),
                this.consensusManager);

            this.blockObserver = new BlockObserver(
                this.federationWalletSyncManager,
                this.depositExtractor,
                this.withdrawalExtractor,
                this.withdrawalReceiver,
                this.federationGatewayClient,
                this.maturedBlocksProvider);
        }

        [Fact]
        public void BlockObserver_Should_Not_Try_To_Extract_Deposits_Before_MinimumDepositConfirmations()
        {
            int confirmations = (int)this.minimumDepositConfirmations - 1;

            var earlyBlock = new Block();
            var earlyChainHeaderBlock = new ChainedHeaderBlock(earlyBlock, new ChainedHeader(new BlockHeader(), uint256.Zero, confirmations));

            this.blockObserver.OnNext(earlyChainHeaderBlock);

            this.federationWalletSyncManager.Received(1).ProcessBlock(earlyBlock);
            this.withdrawalExtractor.ReceivedWithAnyArgs(1).ExtractWithdrawalsFromBlock(earlyBlock, 0);
            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(this.extractedWithdrawals));
            this.federationGatewayClient.ReceivedWithAnyArgs(1).PushCurrentBlockTipAsync(null);

            // TODO
            //this.federationGatewayClient.ReceivedWithAnyArgs(0).PushMaturedBlockAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Try_To_Extract_Deposits_After_MinimumDepositConfirmations()
        {
            (ChainedHeaderBlock chainedHeaderBlock, Block block) blockBuilder = this.ChainHeaderBlockBuilder();

            this.blockObserver.OnNext(blockBuilder.chainedHeaderBlock);

            this.federationWalletSyncManager.Received(1).ProcessBlock(blockBuilder.block);
            // TODO
            //this.federationGatewayClient.ReceivedWithAnyArgs(1).PushMaturedBlockAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Send_Block_Tip()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnNext(chainedHeaderBlock);
            this.federationGatewayClient.ReceivedWithAnyArgs(1).PushCurrentBlockTipAsync(null);
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
