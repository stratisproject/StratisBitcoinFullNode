using System;
using System.Collections.Generic;
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
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PosMintingTest : LogsTestBase, IDisposable
    {
        private PosMinting posMinting;
        private readonly bool initialBlockSignature;
        private readonly bool initialTimestamp;
        private readonly Mock<IPosConsensusValidator> consensusValidator;
        private readonly Mock<IConsensusLoop> consensusLoop;
        private ConcurrentChain chain;
        private readonly Network network;
        private readonly Mock<IConnectionManager> connectionManager;
        private readonly Mock<IDateTimeProvider> dateTimeProvider;
        private readonly Mock<IInitialBlockDownloadState> initialBlockDownloadState;
        private readonly Mock<INodeLifetime> nodeLifetime;
        private readonly Mock<CoinView> coinView;
        private readonly Mock<IStakeChain> stakeChain;
        private readonly List<uint256> powBlocks;
        private readonly Mock<IStakeValidator> stakeValidator;
        private readonly MempoolSchedulerLock mempoolSchedulerLock;
        private readonly Mock<ITxMempool> txMempool;
        private readonly Mock<IWalletManager> walletManager;
        private readonly Mock<IAsyncLoopFactory> asyncLoopFactory;
        private readonly Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;
        private readonly CancellationTokenSource cancellationTokenSource;

        public PosMintingTest()
        {
            this.initialBlockSignature = Block.BlockSignature;
            this.initialTimestamp = Transaction.TimeStamp;

            Transaction.TimeStamp = true;
            Block.BlockSignature = true;

            this.consensusValidator = new Mock<IPosConsensusValidator>();
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.network = Network.StratisTest;
            this.network.Consensus.Options = new PowConsensusOptions();
            this.chain = new ConcurrentChain(this.network);
            this.connectionManager = new Mock<IConnectionManager>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.coinView = new Mock<CoinView>();
            this.stakeChain = new Mock<IStakeChain>();
            this.powBlocks = new List<uint256>();
            this.SetupStakeChain();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.mempoolSchedulerLock = new MempoolSchedulerLock();
            this.txMempool = new Mock<ITxMempool>();
            this.walletManager = new Mock<IWalletManager>();
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();

            this.consensusLoop.Setup(c => c.Validator).Returns(this.consensusValidator.Object);

            this.cancellationTokenSource = new CancellationTokenSource();
            this.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(this.cancellationTokenSource.Token);

            this.posMinting = this.InitializePosMinting();
        }

        public void Dispose()
        {
            Block.BlockSignature = this.initialBlockSignature;
            Transaction.TimeStamp = this.initialTimestamp;
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

            var model = this.posMinting.GetGetStakingInfoModel();
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

            var model = this.posMinting.GetGetStakingInfoModel();
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
            var model = this.posMinting.GetGetStakingInfoModel();
            Assert.Null(model.Errors);
            Assert.False(model.Enabled);
        }

        // the difficulty tests are ported from: https://github.com/bitcoin/bitcoin/blob/3e1ee310437f4c93113f6121425beffdc94702c2/src/test/blockchain_tests.cpp
        [Fact]
        public void GetDifficulty_VeryLowTarget_ReturnsDifficulty()
        {
            ChainedBlock chainedBlock = CreateChainedBlockWithNBits(0x1f111111);

            var result = this.posMinting.GetDifficulty(chainedBlock);

            Assert.Equal(0.000001, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_LowTarget_ReturnsDifficulty()
        {
            ChainedBlock chainedBlock = CreateChainedBlockWithNBits(0x1ef88f6f);

            var result = this.posMinting.GetDifficulty(chainedBlock);

            Assert.Equal(0.000016, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_MidTarget_ReturnsDifficulty()
        {
            ChainedBlock chainedBlock = CreateChainedBlockWithNBits(0x1df88f6f);

            var result = this.posMinting.GetDifficulty(chainedBlock);

            Assert.Equal(0.004023, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_HighTarget_ReturnsDifficulty()
        {
            ChainedBlock chainedBlock = CreateChainedBlockWithNBits(0x1cf88f6f);

            var result = this.posMinting.GetDifficulty(chainedBlock);

            Assert.Equal(1.029916, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_VeryHighTarget_ReturnsDifficulty()
        {
            ChainedBlock chainedBlock = CreateChainedBlockWithNBits(0x12345678);

            var result = this.posMinting.GetDifficulty(chainedBlock);

            Assert.Equal(5913134931067755359633408.0, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_BlockNull_UsesConsensusLoopTipAndStakeValidator_FindsBlock_ReturnsDifficulty()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            ChainedBlock chainedBlock = CreateChainedBlockWithNBits(0x12345678);
            this.stakeValidator.Setup(s => s.GetLastPowPosChainedBlock(this.stakeChain.Object, It.Is<ChainedBlock>(c => c.HashBlock == this.chain.Tip.HashBlock), false))
                .Returns(chainedBlock);

            this.posMinting = this.InitializePosMinting();
            var result = this.posMinting.GetDifficulty(null);

            Assert.Equal(5913134931067755359633408.0, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_BlockNull_NoConsensusTip_ReturnsDefaultDifficulty()
        {
            this.consensusLoop.Setup(c => c.Tip)
                .Returns((ChainedBlock)null);

            var result = this.posMinting.GetDifficulty(null);

            Assert.Equal(1, result);
        }

        [Fact]
        public void GetNetworkWeight_NoConsensusLoopTip_ReturnsZero()
        {
            this.consensusLoop.Setup(c => c.Tip)
                .Returns((ChainedBlock)null);

            var result = this.posMinting.GetNetworkWeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetNetworkWeight_UsingConsensusLoop_HavingMoreThan73Blocks_CalculatesNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(75, this.network, 60, 0x1df88f6f);
            this.InitializePosMinting();
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            var weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4607763.9659653762, weight);
        }

        [Fact]
        public void GetNetworkWeight_UsingConsensusLoop_HavingLessThan73Blocks_CalculatesNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(50, this.network, 60, 0x1df88f6f);
            this.InitializePosMinting();
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            var weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4701799.9652707893, weight);
        }

        [Fact]
        public void GetNetworkWeight_NonPosBlocksInbetweenPosBlocks_SkipsPowBlocks_CalculatedNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(73, this.network, 60, 0x1df88f6f);
            // the following non-pos blocks should be excluded.
            AddBlockToChainWithBlockTimeAndDifficulty(this.chain, 3, 60, 0x12345678);

            foreach (int blockHeight in new int[] { 74, 75, 76 })
            {
                var blockHash = this.chain.GetBlock(blockHeight).HashBlock;
                this.powBlocks.Add(blockHash);
            }

            this.InitializePosMinting();
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            var weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4607763.9659653762, weight);
        }

        [Fact]
        public void GetNetworkWeight_UsesLast73Blocks_CalculatedNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(5, this.network, 60, 0x12345678);
            // only the last 72 blocks should be included. 
            // it skips the first block because it cannot determine it for a single block so we need to add 73.
            AddBlockToChainWithBlockTimeAndDifficulty(this.chain, 73, 60, 0x1df88f6f);
            this.InitializePosMinting();
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            var weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4607763.9659653762, weight);
        }

        private static void AddBlockToChainWithBlockTimeAndDifficulty(ConcurrentChain chain, int blockAmount, int incrementSeconds, uint nbits)
        {
            var prevBlockHash = chain.Tip.HashBlock;
            var nonce = RandomUtils.GetUInt32();
            var blockTime = Utils.UnixTimeToDateTime(chain.Tip.Header.Time).UtcDateTime;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(blockTime);
                blockTime = blockTime.AddSeconds(incrementSeconds);
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                block.Header.Bits = new Target(nbits);
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }
        }

        public static ConcurrentChain GenerateChainWithBlockTimeAndHeight(int blockAmount, Network network, int incrementSeconds, uint nbits)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            var blockTime = Utils.UnixTimeToDateTime(chain.Genesis.Header.Time).UtcDateTime;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(blockTime);
                blockTime = blockTime.AddSeconds(incrementSeconds);
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                block.Header.Bits = new Target(nbits);
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private void SetupStakeChain()
        {
            uint256 callbackBlockId = new uint256();
            this.stakeChain.Setup(s => s.Get(It.IsAny<uint256>()))
                .Callback<uint256>((b) => { callbackBlockId = b; })
                .Returns(() =>
                {
                    var blockStake = new BlockStake();

                    if (!this.powBlocks.Contains(callbackBlockId))
                    {
                        blockStake.Flags = BlockFlag.BLOCK_PROOF_OF_STAKE;
                    }

                    return blockStake;
                });
        }

        private PosMinting InitializePosMinting()
        {
            var posBlockBuilder = new Mock<PosBlockAssembler>(
                this.consensusLoop.Object,
                this.dateTimeProvider.Object,
                this.LoggerFactory.Object,
                this.txMempool.Object,
                this.mempoolSchedulerLock,
                this.network,
                this.stakeChain.Object,
                this.stakeValidator.Object);

            return new PosMinting(
                posBlockBuilder.Object,
                this.consensusLoop.Object,
                this.chain,
                this.network,
                this.connectionManager.Object,
                this.dateTimeProvider.Object,
                this.initialBlockDownloadState.Object,
                this.nodeLifetime.Object,
                this.coinView.Object,
                this.stakeChain.Object,
                this.stakeValidator.Object,
                this.mempoolSchedulerLock,
                this.txMempool.Object,
                this.walletManager.Object,
                this.asyncLoopFactory.Object,
                this.timeSyncBehaviorState.Object,
                this.LoggerFactory.Object);
        }

        private static ChainedBlock CreateChainedBlockWithNBits(uint bits)
        {
            var blockHeader = new BlockHeader();
            blockHeader.Time = 1269211443;
            blockHeader.Bits = new Target(bits);
            var chainedBlock = new ChainedBlock(blockHeader, blockHeader.GetHash(), 46367);
            return chainedBlock;
        }
    }
}