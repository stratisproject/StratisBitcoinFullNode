using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PosMintingTest : LogsTestBase
    {
        protected PosMinting posMinting;
        private readonly Mock<IConsensusManager> consensusManager;
        private ConcurrentChain chain;
        protected Network network;
        private readonly Mock<IDateTimeProvider> dateTimeProvider;
        private readonly Mock<IInitialBlockDownloadState> initialBlockDownloadState;
        private readonly Mock<INodeLifetime> nodeLifetime;
        private readonly Mock<ICoinView> coinView;
        private readonly Mock<IStakeChain> stakeChain;
        private readonly List<uint256> powBlocks;
        private readonly Mock<IStakeValidator> stakeValidator;
        private readonly MempoolSchedulerLock mempoolSchedulerLock;
        private readonly Mock<ITxMempool> txMempool;
        private readonly MinerSettings minerSettings;
        private readonly Mock<IWalletManager> walletManager;
        private readonly Mock<IAsyncLoopFactory> asyncLoopFactory;
        private readonly Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;
        private readonly CancellationTokenSource cancellationTokenSource;

        public PosMintingTest()
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.network = KnownNetworks.StratisTest;
            this.network.Consensus.Options = new ConsensusOptions();
            this.chain = new ConcurrentChain(this.network);
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.coinView = new Mock<ICoinView>();
            this.stakeChain = new Mock<IStakeChain>();
            this.powBlocks = new List<uint256>();
            this.SetupStakeChain();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.mempoolSchedulerLock = new MempoolSchedulerLock();
            this.minerSettings = new MinerSettings(NodeSettings.Default(this.network));
            this.txMempool = new Mock<ITxMempool>();
            this.walletManager = new Mock<IWalletManager>();
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();

            this.cancellationTokenSource = new CancellationTokenSource();
            this.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(this.cancellationTokenSource.Token);

            this.posMinting = this.InitializePosMinting();
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

            this.posMinting.Stake(new WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });

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

            bool isSystemTimeOutOfSyncCalled = false;
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

            this.posMinting.Stake(new WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            asyncLoop.Run();

            GetStakingInfoModel model = this.posMinting.GetGetStakingInfoModel();
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

            bool isSystemTimeOutOfSyncCalled = false;
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

            this.posMinting.Stake(new WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            asyncLoop.Run();

            GetStakingInfoModel model = this.posMinting.GetGetStakingInfoModel();
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


            bool isSystemTimeOutOfSyncCalled = false;
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

            this.posMinting.Stake(new WalletSecret() { WalletName = "wallet1", WalletPassword = "myPassword" });
            stakingLoopFunction(stakingLoopToken);
            stakingLoopFunction(stakingLoopToken);

            Assert.True(stakingLoopToken.IsCancellationRequested);
            asyncLoop.Verify(a => a.Dispose());
            GetStakingInfoModel model = this.posMinting.GetGetStakingInfoModel();
            Assert.Null(model.Errors);
            Assert.False(model.Enabled);
        }

        [Fact]
        public async Task GenerateBlocksAsync_does_not_use_small_coins()
        {
            var walletSecret = new WalletSecret() { WalletName = "wallet", WalletPassword = "password" };
            var wallet = new Wallet.Wallet()
            {
                Network = this.network
            };
            var milliseconds550MinutesAgo = (uint)Math.Max(this.chain.Tip.Header.Time - TimeSpan.FromMinutes(550).Milliseconds, 0);
            this.AddAccountWithSpendableOutputs(wallet);
            var spendableTransactions = wallet.GetAllSpendableTransactions(CoinType.Stratis, this.chain.Tip.Height, 0).ToList();

            this.walletManager.Setup(w => w.GetSpendableTransactionsInWalletForStaking(It.IsAny<string>(), It.IsAny<int>()))
                .Returns(spendableTransactions);

            var fetchedUtxos = spendableTransactions
                .Select(t => new UnspentOutputs(t.Transaction.Id, new Coins()
                {
                    CoinBase = false,
                    CoinStake = false,
                    Height = 0,
                    Outputs = { new TxOut(t.Transaction.Amount ?? Money.Zero, t.Address.ScriptPubKey) },
                    Time = milliseconds550MinutesAgo,
                    Version = 1
                }))
                .ToArray();
            var fetchCoinsResponse = new FetchCoinsResponse(fetchedUtxos, this.chain.Tip.HashBlock);

            fetchCoinsResponse.UnspentOutputs
                .Where(u => u.Outputs.Any(o => o.Value < this.posMinting.MinimumStakingCoinValue)).Should()
                .NotBeEmpty("otherwise we are not sure the code actually excludes them");
            fetchCoinsResponse.UnspentOutputs
                .Where(u => u.Outputs.Any(o => o.Value >= this.posMinting.MinimumStakingCoinValue)).Should()
                .NotBeEmpty("otherwise we are not sure the code actually includes them");

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(fetchCoinsResponse));

            this.consensusManager.Setup(c => c.Tip).Returns(this.chain.Tip);
            this.dateTimeProvider.Setup(c => c.GetAdjustedTimeAsUnixTimestamp())
                .Returns(this.chain.Tip.Header.Time + 16);
            var ct = CancellationToken.None;
            var utxoStakeDescriptions = await this.posMinting.GetUtxoStakeDescriptionsAsync(walletSecret, ct);


            utxoStakeDescriptions.Select(d => d.TxOut.Value).Where(v => v < this.posMinting.MinimumStakingCoinValue)
                .Should().BeEmpty("small coins should not be included");
            utxoStakeDescriptions.Select(d => d.TxOut.Value).Where(v => v >= this.posMinting.MinimumStakingCoinValue)
                .Should().NotBeEmpty("big enough coins should be included");

            var expectedAmounts = spendableTransactions.Select(s => s.Transaction.Amount)
                .Where(a => a >= this.posMinting.MinimumStakingCoinValue).ToArray();
            utxoStakeDescriptions.Count.Should().Be(expectedAmounts.Length);

            utxoStakeDescriptions.Select(d => d.TxOut.Value).Should().Contain(expectedAmounts);
        }

        private void AddAccountWithSpendableOutputs(Wallet.Wallet wallet)
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 0, Amount = this.posMinting.MinimumStakingCoinValue - 1 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(16), Index = 0, Amount = this.posMinting.MinimumStakingCoinValue } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(17), Index = 0, Amount = 2 * Money.COIN } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 0, Amount = 2 * Money.CENT } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 0, Amount = 1 * Money.NANO } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 4, Transactions = null });
            wallet.AccountsRoot.Add(new AccountRoot() { Accounts = new[] { account }, CoinType = CoinType.Stratis });
        }

        // the difficulty tests are ported from: https://github.com/bitcoin/bitcoin/blob/3e1ee310437f4c93113f6121425beffdc94702c2/src/test/blockchain_tests.cpp
        [Fact]
        public void GetDifficulty_VeryLowTarget_ReturnsDifficulty()
        {
            ChainedHeader chainedHeader = CreateChainedBlockWithNBits(this.network, 0x1f111111);

            double result = this.posMinting.GetDifficulty(chainedHeader);

            Assert.Equal(0.000001, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_LowTarget_ReturnsDifficulty()
        {
            ChainedHeader chainedHeader = CreateChainedBlockWithNBits(this.network, 0x1ef88f6f);

            double result = this.posMinting.GetDifficulty(chainedHeader);

            Assert.Equal(0.000016, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_MidTarget_ReturnsDifficulty()
        {
            ChainedHeader chainedHeader = CreateChainedBlockWithNBits(this.network, 0x1df88f6f);

            double result = this.posMinting.GetDifficulty(chainedHeader);

            Assert.Equal(0.004023, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_HighTarget_ReturnsDifficulty()
        {
            ChainedHeader chainedHeader = CreateChainedBlockWithNBits(this.network, 0x1cf88f6f);

            double result = this.posMinting.GetDifficulty(chainedHeader);

            Assert.Equal(1.029916, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_VeryHighTarget_ReturnsDifficulty()
        {
            ChainedHeader chainedHeader = CreateChainedBlockWithNBits(this.network, 0x12345678);

            double result = this.posMinting.GetDifficulty(chainedHeader);

            Assert.Equal(5913134931067755359633408.0, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_BlockNull_UsesConsensusLoopTipAndStakeValidator_FindsBlock_ReturnsDifficulty()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.consensusManager.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            ChainedHeader chainedHeader = CreateChainedBlockWithNBits(this.network, 0x12345678);
            this.stakeValidator.Setup(s => s.GetLastPowPosChainedBlock(this.stakeChain.Object, It.Is<ChainedHeader>(c => c.HashBlock == this.chain.Tip.HashBlock), false))
                .Returns(chainedHeader);

            this.posMinting = this.InitializePosMinting();
            double result = this.posMinting.GetDifficulty(null);

            Assert.Equal(5913134931067755359633408.0, Math.Round(result, 6));
        }

        [Fact]
        public void GetDifficulty_BlockNull_NoConsensusTip_ReturnsDefaultDifficulty()
        {
            this.consensusManager.Setup(c => c.Tip)
                .Returns((ChainedHeader)null);

            double result = this.posMinting.GetDifficulty(null);

            Assert.Equal(1, result);
        }

        [Fact]
        public void GetNetworkWeight_NoConsensusLoopTip_ReturnsZero()
        {
            this.consensusManager.Setup(c => c.Tip)
                .Returns((ChainedHeader)null);

            double result = this.posMinting.GetNetworkWeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetNetworkWeight_UsingConsensusLoop_HavingMoreThan73Blocks_CalculatesNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(75, this.network, 60, 0x1df88f6f);
            this.InitializePosMinting();
            this.consensusManager.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            double weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4607763.9659653762, weight);
        }

        [Fact]
        public void GetNetworkWeight_UsingConsensusLoop_HavingLessThan73Blocks_CalculatesNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(50, this.network, 60, 0x1df88f6f);
            this.InitializePosMinting();
            this.consensusManager.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            double weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4701799.9652707893, weight);
        }

        [Fact]
        public void GetNetworkWeight_NonPosBlocksInbetweenPosBlocks_SkipsPowBlocks_CalculatedNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(73, this.network, 60, 0x1df88f6f);
            // the following non-pos blocks should be excluded.
            AddBlockToChainWithBlockTimeAndDifficulty(this.chain, 3, 60, 0x12345678, this.network);

            foreach (int blockHeight in new int[] { 74, 75, 76 })
            {
                uint256 blockHash = this.chain.GetBlock(blockHeight).HashBlock;
                this.powBlocks.Add(blockHash);
            }

            this.InitializePosMinting();
            this.consensusManager.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            double weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4607763.9659653762, weight);
        }

        [Fact]
        public void GetNetworkWeight_UsesLast73Blocks_CalculatedNetworkWeightUsingLatestBlocks()
        {
            this.chain = GenerateChainWithBlockTimeAndHeight(5, this.network, 60, 0x12345678);
            // only the last 72 blocks should be included.
            // it skips the first block because it cannot determine it for a single block so we need to add 73.
            AddBlockToChainWithBlockTimeAndDifficulty(this.chain, 73, 60, 0x1df88f6f, this.network);
            this.InitializePosMinting();
            this.consensusManager.Setup(c => c.Tip)
                .Returns(this.chain.Tip);

            double weight = this.posMinting.GetNetworkWeight();

            Assert.Equal(4607763.9659653762, weight);
        }

        [Fact]
        public void CoinstakeAge_BeforeActivation_Testnet()
        {
            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, 1000, 1000 - 8)); // utxo depth is 9, mining block at 10

            Assert.False(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, 1000, 1000 - 7)); // utxo depth is 8, mining block at 9
        }

        /// <summary>This is a test of coinstake age softfork activation on testnet.</summary>
        /// <remarks><see cref="PosConsensusOptions.GetStakeMinConfirmations"/></remarks>
        [Fact]
        public void CoinstakeAge_AfterActivation_Testnet()
        {
            int activationHeight = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightTestnet;
            int afterActivationHeight = activationHeight + 1000;

            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, afterActivationHeight, afterActivationHeight - 18));

            Assert.False(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, afterActivationHeight, afterActivationHeight - 17));
        }

        /// <summary>This is a test of coinstake age softfork activation on testnet.</summary>
        /// <remarks><see cref="PosConsensusOptions.GetStakeMinConfirmations"/></remarks>
        [Fact]
        public void CoinstakeAge_AtTheActivation_Testnet()
        {
            int activationHeight = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightTestnet;

            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, activationHeight - 2, activationHeight - 10)); // mining block before activation

            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, activationHeight - 1, activationHeight - 19)); // mining activation block

            Assert.False(this.WasUtxoSelectedForStaking(KnownNetworks.StratisTest, activationHeight - 1, activationHeight - 18)); // mining activation block
        }

        /// <summary>This is a test of coinstake age softfork activation on mainnet.</summary>
        /// <remarks><see cref="PosConsensusOptions.GetStakeMinConfirmations"/></remarks>
        [Fact]
        public void CoinstakeAge_BeforeActivation_Mainnet()
        {
            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, 1000, 1000 - 48)); // utxo depth is 49, mining block at 50

            Assert.False(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, 1000, 1000 - 47)); // utxo depth is 48, mining block at 49
        }

        /// <summary>This is a test of coinstake age softfork activation on mainnet.</summary>
        /// <remarks><see cref="PosConsensusOptions.GetStakeMinConfirmations"/></remarks>
        [Fact]
        public void CoinstakeAge_AfterActivation_Mainnet()
        {
            int activationHeight = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightMainnet;
            int afterActivationHeight = activationHeight + 1000;

            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, afterActivationHeight, afterActivationHeight - 498));

            Assert.False(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, afterActivationHeight, afterActivationHeight - 497));
        }

        /// <summary>This is a test of coinstake age softfork activation on mainnet.</summary>
        /// <remarks><see cref="PosConsensusOptions.GetStakeMinConfirmations"/></remarks>
        [Fact]
        public void CoinstakeAge_AtTheActivation_Mainnet()
        {
            int activationHeight = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightMainnet;

            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, activationHeight - 2, activationHeight - 50)); // mining block before activation

            Assert.True(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, activationHeight - 1, activationHeight - 499)); // mining activation block

            Assert.False(this.WasUtxoSelectedForStaking(KnownNetworks.StratisMain, activationHeight - 1, activationHeight - 498)); // mining activation block
        }

        private bool WasUtxoSelectedForStaking(Network network, int chainTipHeight, int utxoHeight)
        {
            this.network = network;
            this.network.Consensus.Options = new PosConsensusOptions();
            this.chain = GenerateChainWithBlockTimeAndHeight(2, this.network, 60, 0x1df88f6f);

            PosMinting miner = this.InitializePosMinting();

            ChainedHeader chainTip = this.chain.Tip;
            chainTip.SetPrivatePropertyValue("Height", chainTipHeight);
            chainTip.Previous.SetPrivatePropertyValue("Height", utxoHeight);

            var descriptions = new List<UtxoStakeDescription>();

            var utxoDescription = new UtxoStakeDescription();
            utxoDescription.TxOut = new TxOut(new Money(100), new Mock<IDestination>().Object);
            utxoDescription.OutPoint = new OutPoint(uint256.One, 0);
            utxoDescription.HashBlock = chainTip.Previous.HashBlock;

            utxoDescription.UtxoSet = new UnspentOutputs();
            utxoDescription.UtxoSet.SetPrivatePropertyValue("Time", chainTip.Header.Time);

            descriptions.Add(utxoDescription);

            List<UtxoStakeDescription> suitableCoins = miner.GetUtxoStakeDescriptionsSuitableForStakingAsync(descriptions, chainTip, chainTip.Header.Time + 64, long.MaxValue).GetAwaiter().GetResult();
            return suitableCoins.Count == 1;
        }

        private static void AddBlockToChainWithBlockTimeAndDifficulty(ConcurrentChain chain, int blockAmount, int incrementSeconds, uint nbits, Network network)
        {
            uint256 prevBlockHash = chain.Tip.HashBlock;
            uint nonce = RandomUtils.GetUInt32();
            DateTime blockTime = Utils.UnixTimeToDateTime(chain.Tip.Header.Time).UtcDateTime;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
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
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            DateTime blockTime = Utils.UnixTimeToDateTime(chain.Genesis.Header.Time).UtcDateTime;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
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
            var callbackBlockId = new uint256();
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
            var posBlockAssembler = new Mock<PosBlockDefinition>(
                this.consensusManager.Object,
                this.dateTimeProvider.Object,
                this.LoggerFactory.Object,
                this.txMempool.Object,
                this.mempoolSchedulerLock,
                this.minerSettings,
                this.network,
                this.stakeChain.Object,
                this.stakeValidator.Object);

            posBlockAssembler.Setup(a => a.Build(It.IsAny<ChainedHeader>(), It.IsAny<Script>()))
                .Returns(new BlockTemplate(this.network));
            var blockBuilder = new MockPosBlockProvider(posBlockAssembler.Object);

            return new PosMinting(
                blockBuilder,
                this.consensusManager.Object,
                this.chain,
                this.network,
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
                this.LoggerFactory.Object,
                this.minerSettings);
        }

        private static ChainedHeader CreateChainedBlockWithNBits(Network network, uint bits)
        {
            BlockHeader blockHeader = network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.Time = 1269211443;
            blockHeader.Bits = new Target(bits);
            var chainedHeader = new ChainedHeader(blockHeader, blockHeader.GetHash(), 46367);
            return chainedHeader;
        }
    }

    public sealed class MockPosBlockProvider : IBlockProvider
    {
        private readonly PosBlockDefinition blockDefinition;

        public MockPosBlockProvider(PosBlockDefinition blockDefinition)
        {
            this.blockDefinition = blockDefinition;
        }

        public BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script)
        {
            return this.blockDefinition.Build(chainTip, script);
        }

        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            throw new NotImplementedException();
        }
    }
}