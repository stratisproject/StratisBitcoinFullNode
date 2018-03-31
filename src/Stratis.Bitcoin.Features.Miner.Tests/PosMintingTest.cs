using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PosMintingTest : LogsTestBase, IDisposable
    {
        private PosMinting posMinting;
        private bool initialBlockSignature;
        private Mock<IPosConsensusValidator> consensusValidator;
        private Mock<IConsensusLoop> consensusLoop;
        private ConcurrentChain chain;
        private Network network;
        private Mock<IConnectionManager> connectionManager;
        private Mock<IDateTimeProvider> dateTimeProvider;
        private Mock<IAssemblerFactory> assemblerFactory;
        private Mock<IInitialBlockDownloadState> initialBlockDownloadState;
        private Mock<INodeLifetime> nodeLifetime;
        private TestCoinView coinView;
        private Mock<IStakeChain> stakeChain;
        private Mock<IStakeValidator> stakeValidator;
        private MempoolSchedulerLock mempoolSchedulerLock;
        private Mock<ITxMempool> txMempool;
        private Mock<IWalletManager> walletManager;
        private Mock<IAsyncLoopFactory> asyncLoopFactory;
        private Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;
        private CancellationTokenSource cancellationTokenSource;

        public PosMintingTest()
        {
            this.initialBlockSignature = Block.BlockSignature;
            Block.BlockSignature = true;

            this.consensusValidator = new Mock<IPosConsensusValidator>();
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.chain = new ConcurrentChain();
            this.network = Network.StratisTest;
            this.connectionManager = new Mock<IConnectionManager>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.assemblerFactory = new Mock<IAssemblerFactory>();
            this.initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.coinView = new TestCoinView();
            this.stakeChain = new Mock<IStakeChain>();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.mempoolSchedulerLock = new MempoolSchedulerLock();
            this.txMempool = new Mock<ITxMempool>();
            this.walletManager = new Mock<IWalletManager>();
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();

            this.consensusLoop.Setup(c => c.Validator)
                .Returns(this.consensusValidator.Object);

            this.cancellationTokenSource = new CancellationTokenSource();
            this.nodeLifetime.Setup(n => n.ApplicationStopping)
                .Returns(this.cancellationTokenSource.Token);

            this.posMinting = new PosMinting(this.consensusLoop.Object, this.chain, this.network, this.connectionManager.Object,
                this.dateTimeProvider.Object, this.assemblerFactory.Object,
                this.initialBlockDownloadState.Object, this.nodeLifetime.Object, this.coinView, this.stakeChain.Object,
                this.stakeValidator.Object, this.mempoolSchedulerLock, this.txMempool.Object,
                this.walletManager.Object, this.asyncLoopFactory.Object, this.timeSyncBehaviorState.Object, this.LoggerFactory.Object);
        }

        public void Dispose()
        {
            Block.BlockSignature = this.initialBlockSignature;
        }


        [Fact]
        public void Stake_StakingLoopNotStarted_StartsStakingLoop()
        {
            var asyncLoop = new AsyncLoop("PosMining.Stake2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; });
            this.asyncLoopFactory.Setup(a => a.Run("PosMining.Stake",
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                It.Is<TimeSpan>(t => t.Milliseconds == 500), TimeSpans.Second))
                .Returns(asyncLoop)
                .Verifiable();

            this.posMinting.Stake(new PosMinting.WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            
            this.nodeLifetime.Verify();
            this.asyncLoopFactory.Verify();
        }

        [Fact]
        public void Stake_StakingLoopThrowsMinerException_AddsErrorToRpcStakingInfoModel()
        {
            var asyncLoop = new AsyncLoop("PosMining.Stake2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; });
            this.asyncLoopFactory.Setup(a => a.Run("PosMining.Stake",
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                It.Is<TimeSpan>(t => t.Milliseconds == 500), TimeSpans.Second))
                        .Callback<string, Func<CancellationToken, Task>, CancellationToken, TimeSpan?, TimeSpan?>((name, func, token, repeat, start) =>
                        {
                            func(token);
                        })
                .Returns(asyncLoop)
                .Verifiable();

            var isSystemTimeOutOfSyncCalled = false;
            this.timeSyncBehaviorState.Setup(c => c.IsSystemTimeOutOfSync)
                .Returns(() =>
                {
                    if (!isSystemTimeOutOfSyncCalled)
                    {
                        isSystemTimeOutOfSyncCalled = true;
                        throw new MinerException("Mining error.");
                    }
                    this.cancellationTokenSource.Cancel();
                    throw new InvalidOperationException("End the loop");
                });

            this.posMinting.Stake(new PosMinting.WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            asyncLoop.Run();

            var model = this.posMinting.GetStakingInfoModel();
            Assert.Equal("Mining error.", model.Errors);
        }

        [Fact]
        public void Stake_StakingLoopThrowsConsensusErrorException_AddsErrorToRpcStakingInfoModel()
        {
            var asyncLoop = new AsyncLoop("PosMining.Stake2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; });
            this.asyncLoopFactory.Setup(a => a.Run("PosMining.Stake",
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                It.Is<TimeSpan>(t => t.Milliseconds == 500), TimeSpans.Second))
                 .Callback<string, Func<CancellationToken, Task>, CancellationToken, TimeSpan?, TimeSpan?>((name, func, token, repeat, start) =>
                 {
                     func(token);
                 })
                .Returns(asyncLoop)
                .Verifiable();

            var isSystemTimeOutOfSyncCalled = false;
            this.timeSyncBehaviorState.Setup(c => c.IsSystemTimeOutOfSync)
                .Returns(() =>
                {
                    if (!isSystemTimeOutOfSyncCalled)
                    {
                        isSystemTimeOutOfSyncCalled = true;
                        throw new ConsensusErrorException(new ConsensusError("15", "Consensus error."));
                    }
                    this.cancellationTokenSource.Cancel();
                    throw new InvalidOperationException("End the loop");
                });

            this.posMinting.Stake(new PosMinting.WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            asyncLoop.Run();

            var model = this.posMinting.GetStakingInfoModel();
            Assert.Equal("Consensus error.", model.Errors);
        }

        [Fact]
        public void StopStake_DisposesResources()
        {
            var asyncLoop = new Mock<IAsyncLoop>();

            Func<CancellationToken, Task> stakingLoopFunction = null;
            CancellationToken stakingLoopToken = default(CancellationToken);
            this.asyncLoopFactory.Setup(a => a.Run("PosMining.Stake",
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                It.Is<TimeSpan>(t => t.Milliseconds == 500), TimeSpans.Second))
                 .Callback<string, Func<CancellationToken, Task>, CancellationToken, TimeSpan?, TimeSpan?>((name, func, token, repeat, start) =>
                 {
                     stakingLoopFunction = func;
                     stakingLoopToken = token;
                 })
                .Returns(asyncLoop.Object)
                .Verifiable();


            var isSystemTimeOutOfSyncCalled = false;
            this.timeSyncBehaviorState.Setup(c => c.IsSystemTimeOutOfSync)
                .Returns(() =>
                {
                    if (!isSystemTimeOutOfSyncCalled)
                    {
                        isSystemTimeOutOfSyncCalled = true;
                        // generates an error in the stakinginfomodel.
                        throw new MinerException("Mining error.");
                    }

                    this.posMinting.StopStake();// stop the staking.
                    throw new InvalidOperationException("End the loop");
                });

            this.posMinting.Stake(new PosMinting.WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            stakingLoopFunction(stakingLoopToken);
            stakingLoopFunction(stakingLoopToken);

            Assert.True(stakingLoopToken.IsCancellationRequested);
            asyncLoop.Verify(a => a.Dispose());
            var model = this.posMinting.GetStakingInfoModel();
            Assert.Null(model.Errors);
            Assert.False(model.Enabled);
        }      

        private class TestCoinView : CoinView
        {
            public TestCoinView() : base()
            {
            }

            public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
            {
                throw new NotImplementedException();
            }

            public override Task<uint256> Rewind()
            {
                throw new NotImplementedException();
            }

            public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
            {
                throw new NotImplementedException();
            }
        }
    }
}
