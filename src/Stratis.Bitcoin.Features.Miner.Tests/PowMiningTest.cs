using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    // ========================
    // TODO fix this tests
    // ========================

    public class PowMiningTest : LogsTestBase, IClassFixture<PowMiningTestFixture>
    {
        private readonly Mock<IAsyncLoopFactory> asyncLoopFactory;
        private ConcurrentChain chain;
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly Mock<IConsensusRuleEngine> consensusRules;
        private readonly Mock<IInitialBlockDownloadState> initialBlockDownloadState;
        private readonly ConsensusOptions initialNetworkOptions;
        private readonly PowMiningTestFixture fixture;
        private readonly Mock<ITxMempool> mempool;
        private readonly MempoolSchedulerLock mempoolLock;
        private readonly MinerSettings minerSettings;
        private readonly Network network;
        private readonly Mock<INodeLifetime> nodeLifetime;
        public PowMiningTest(PowMiningTestFixture fixture)
        {
            this.fixture = fixture;
            this.network = fixture.Network;

            this.initialNetworkOptions = this.network.Consensus.Options;
            if (this.initialNetworkOptions == null)
                this.network.Consensus.Options = new ConsensusOptions();

            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();

            this.consensusManager = new Mock<IConsensusManager>();
            this.consensusManager.SetupGet(c => c.Tip).Returns(() => this.chain.Tip);
            this.consensusRules = new Mock<IConsensusRuleEngine>();

            this.mempool = new Mock<ITxMempool>();
            this.mempool.SetupGet(mp => mp.MapTx).Returns(new TxMempool.IndexedTransactionSet());

            this.minerSettings = new MinerSettings(NodeSettings.Default(this.network));

            this.chain = fixture.Chain;

            this.nodeLifetime = new Mock<INodeLifetime>();
            this.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(new CancellationToken()).Verifiable();

            this.initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();
            this.initialBlockDownloadState.Setup(s => s.IsInitialBlockDownload()).Returns(false);

            this.mempoolLock = new MempoolSchedulerLock();
        }

        [Fact]
        public void Mine_FirstCall_CreatesNewMiningLoop_ReturnsMiningLoop()
        {
            this.asyncLoopFactory.Setup(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.Second, TimeSpans.TenSeconds))
                .Returns(new AsyncLoop("PowMining.Mine2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; }))
                .Verifiable();

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);

            miner.Mine(new Key().ScriptPubKey);

            this.nodeLifetime.Verify();
            this.asyncLoopFactory.Verify();
        }

        [Fact]
        public void Mine_SecondCall_ReturnsSameMiningLoop()
        {
            this.asyncLoopFactory.Setup(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.Second, TimeSpans.TenSeconds))
                .Returns(new AsyncLoop("PowMining.Mine2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; }))
                .Verifiable();

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);

            miner.Mine(new Key().ScriptPubKey);
            miner.Mine(new Key().ScriptPubKey);

            this.nodeLifetime.Verify();
            this.asyncLoopFactory.Verify(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.Second, TimeSpans.TenSeconds), Times.Exactly(1));
        }

        [Fact]
        public void Mine_CreatedAsyncLoop_GeneratesBlocksUntilCancelled()
        {
            var cancellationToken = new CancellationToken();
            this.nodeLifetime.SetupSequence(n => n.ApplicationStopping)
              .Returns(cancellationToken)
              .Returns(new CancellationToken(true));

            string callbackName = null;
            Func<CancellationToken, Task> callbackFunc = null;
            TimeSpan? callbackRepeat = null;

            this.asyncLoopFactory.Setup(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.Second, TimeSpans.TenSeconds))
                .Callback<string, Func<CancellationToken, Task>, CancellationToken, TimeSpan?, TimeSpan?>(
                (name, func, token, repeat, startafter) =>
                {
                    callbackName = name;
                    callbackFunc = func;
                    callbackRepeat = repeat;
                })
                .Returns(() =>
                {
                    return new AsyncLoop(callbackName, this.FullNodeLogger.Object, callbackFunc);
                })
                .Verifiable();

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);

            miner.Mine(new Key().ScriptPubKey);
            this.asyncLoopFactory.Verify();
        }

        [Fact]
        public void IncrementExtraNonce_HashPrevBlockNotSameAsBlockHeaderHashPrevBlock_ResetsExtraNonceAndHashPrevBlock_UpdatesCoinBaseTransactionAndMerkleRoot()
        {
            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);

            FieldInfo hashPrevBlockFieldSelector = this.GetHashPrevBlockFieldSelector();
            hashPrevBlockFieldSelector.SetValue(miner, new uint256(15));

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());

            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(transaction);
            block.Header.HashMerkleRoot = new uint256(0);
            block.Header.HashPrevBlock = new uint256(14);
            this.chain = GenerateChainWithHeight(2, this.network);

            int nExtraNonce = 15;
            nExtraNonce = miner.IncrementExtraNonce(block, this.chain.Tip, nExtraNonce);

            Assert.Equal(new uint256(14), hashPrevBlockFieldSelector.GetValue(miner) as uint256);
            Assert.Equal(block.Transactions[0].Inputs[0].ScriptSig, TxIn.CreateCoinbase(3).ScriptSig);
            Assert.NotEqual(new uint256(0), block.Header.HashMerkleRoot);
            Assert.Equal(1, nExtraNonce);
        }

        [Fact]
        public void IncrementExtraNonce_HashPrevBlockNotSameAsBlockHeaderHashPrevBlock_IncrementsExtraNonce_UpdatesCoinBaseTransactionAndMerkleRoot()
        {
            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);

            FieldInfo hashPrevBlockFieldSelector = this.GetHashPrevBlockFieldSelector();
            hashPrevBlockFieldSelector.SetValue(miner, new uint256(15));

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());

            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(transaction);
            block.Header.HashMerkleRoot = new uint256(0);
            block.Header.HashPrevBlock = new uint256(15);
            this.chain = GenerateChainWithHeight(2, this.network);

            int nExtraNonce = 15;
            nExtraNonce = miner.IncrementExtraNonce(block, this.chain.Tip, nExtraNonce);

            Assert.Equal(block.Transactions[0].Inputs[0].ScriptSig, TxIn.CreateCoinbase(3).ScriptSig);
            Assert.NotEqual(new uint256(0), block.Header.HashMerkleRoot);
            Assert.Equal(16, nExtraNonce);
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_ReturnsGeneratedBlock()
        {
            BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);

            Block callbackBlock = null;
            this.chain.SetTip(this.chain.GetBlock(0));

            this.consensusManager.Setup(c => c.BlockMinedAsync(It.IsAny<Block>()))
                .Callback<Block>((block) => { callbackBlock = block; })
                .ReturnsAsync(new ChainedHeader(blockTemplate.Block.Header, blockTemplate.Block.GetHash(), this.chain.Tip));

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
            List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, uint.MaxValue);

            Assert.NotEmpty(blockHashes);
            Assert.True(blockHashes.Count == 1);
            Assert.Equal(callbackBlock.GetHash(), blockHashes[0]);
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_MaxTriesReached_StopsGeneratingBlocks_ReturnsEmptyList()
        {
            BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);
            this.chain.SetTip(this.chain.GetBlock(0));
            var chainedHeader = new ChainedHeader(blockTemplate.Block.Header, blockTemplate.Block.GetHash(), this.chain.Tip);

            this.consensusManager.Setup(c => c.BlockMinedAsync(It.IsAny<Block>())).ReturnsAsync(chainedHeader);
            blockTemplate.Block.Header.Nonce = 0;
            blockTemplate.Block.Header.Bits = KnownNetworks.TestNet.GetGenesis().Header.Bits; // make the difficulty harder.

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

            PowMining miner = CreateProofOfWorkMiner(blockBuilder.Object);
            List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, 15);

            Assert.Empty(blockHashes);
        }

        [Fact]
        public void GenerateBlocks_ZeroBlocks_ReturnsEmptyList()
        {
            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
            List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 0, int.MaxValue);

            Assert.Empty(blockHashes);
        }

        [Fact]
        public void GenerateBlocks_MultipleBlocks_ReturnsGeneratedBlocks()
        {
            var blocksToValidate = new List<uint256>();
            ChainedHeader lastChainedHeader = null;
            BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);
            var chainedHeader = new ChainedHeader(blockTemplate.Block.Header, blockTemplate.Block.GetHash(), this.chain.Tip);

            this.consensusManager.Setup(c => c.BlockMinedAsync(It.IsAny<Block>()))
                .Callback<Block>((context) =>
                {
                    if (lastChainedHeader == null)
                    {
                        blocksToValidate.Add(this.fixture.ChainedHeader1.HashBlock);
                        lastChainedHeader = this.fixture.ChainedHeader1;
                    }
                    else
                    {
                        blocksToValidate.Add(this.fixture.ChainedHeader2.HashBlock);
                    }

                    this.chain.SetTip(lastChainedHeader);
                })
                .ReturnsAsync(chainedHeader);

            BlockTemplate blockTemplate2 = this.CreateBlockTemplate(this.fixture.Block2);

            this.chain.SetTip(this.chain.GetBlock(0));

            int attempts = 0;

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
            blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript)))
                .Returns(() =>
                {
                    if (lastChainedHeader == null)
                    {
                        if (attempts == 10)
                        {
                            // sometimes the PoW nonce we generate in the fixture is not accepted resulting in an infinite loop. Retry.
                            this.fixture.Block1 = this.fixture.PrepareValidBlock(this.chain.Tip, 1, this.fixture.Key.ScriptPubKey);
                            this.fixture.ChainedHeader1 = new ChainedHeader(this.fixture.Block1.Header, this.fixture.Block1.GetHash(), this.chain.Tip);
                            this.fixture.Block2 = this.fixture.PrepareValidBlock(this.fixture.ChainedHeader1, 2, this.fixture.Key.ScriptPubKey);
                            this.fixture.ChainedHeader2 = new ChainedHeader(this.fixture.Block2.Header, this.fixture.Block2.GetHash(), this.fixture.ChainedHeader1);

                            blockTemplate = CreateBlockTemplate(this.fixture.Block1);
                            blockTemplate2 = CreateBlockTemplate(this.fixture.Block2);
                            attempts = 0;
                        }
                        attempts += 1;

                        return blockTemplate;
                    }

                    return blockTemplate2;
                });

            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
            List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 2, uint.MaxValue);

            Assert.NotEmpty(blockHashes);
            Assert.Equal(2, blockHashes.Count);
            Assert.Equal(blocksToValidate[0], blockHashes[0]);
            Assert.Equal(blocksToValidate[1], blockHashes[1]);
        }

        [Fact]
        public void GenerateBlocks_MultipleBlocks_InvalidPreviousTip_ReturnsValidGeneratedBlocks()
        {
            BlockTemplate block1 = this.CreateBlockTemplate(this.fixture.Block1);

            this.chain.SetTip(this.chain.GetBlock(0));

            var chainedHeader = new ChainedHeader(block1.Block.Header, block1.Block.GetHash(), this.chain.Tip);

            int blockHeight = 0;
            this.consensusManager.Setup(c => c.BlockMinedAsync(It.IsAny<Block>()))
                .ReturnsAsync(() =>
                {
                    blockHeight++;

                    // The second block we mine should "fail" consensus, so we return a null.
                    if (blockHeight == 2)
                        return null;

                    this.chain.SetTip(chainedHeader);

                    return chainedHeader;
                });

            BlockTemplate block2 = this.CreateBlockTemplate(this.fixture.Block2);

            Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();

            // As block 2 will fail consensus we need to still return it as block 3 so that the previous block hash is set properly.
            blockBuilder.SetupSequence(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript)))
                        .Returns(block1)
                        .Returns(block2)
                        .Returns(block2);

            PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);

            // We instruct pass GenerateBlocks to mine 2 valid blocks i.e. it will stop once it has
            // mined 2 blocks that pass consensus.
            List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 2, uint.MaxValue);

            // 3 blocks were mined, 2 passed consensus and 1 failed.
            Assert.True(blockHashes.Count == 2);
            Assert.Equal(chainedHeader.HashBlock, blockHashes[0]);
        }

        private Mock<PowBlockDefinition> CreateProofOfWorkBlockBuilder()
        {
            return new Mock<PowBlockDefinition>(
                    this.consensusManager.Object,
                    DateTimeProvider.Default,
                    this.LoggerFactory.Object,
                    this.mempool.Object,
                    this.mempoolLock,
                    this.minerSettings,
                    this.network,
                    this.consensusRules.Object,
                    null);
        }

        private PowMining CreateProofOfWorkMiner(PowBlockDefinition blockDefinition)
        {
            var blockBuilder = new MockPowBlockProvider(blockDefinition);
            return new PowMining(this.asyncLoopFactory.Object, blockBuilder, this.consensusManager.Object, this.chain, DateTimeProvider.Default, this.mempool.Object, this.mempoolLock, this.network, this.nodeLifetime.Object, this.LoggerFactory.Object, this.initialBlockDownloadState.Object);
        }

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private FieldInfo GetHashPrevBlockFieldSelector()
        {
            Type typeToTest = typeof(PowMining);
            FieldInfo hashPrevBlockFieldSelector = typeToTest.GetField("hashPrevBlock", BindingFlags.Instance | BindingFlags.NonPublic);
            return hashPrevBlockFieldSelector;
        }

        private BlockTemplate CreateBlockTemplate(Block block)
        {
            var blockTemplate = new BlockTemplate(this.network)
            {
                Block = block,
            };
            return blockTemplate;
        }
    }

    public sealed class MockPowBlockProvider : IBlockProvider
    {
        private readonly PowBlockDefinition blockDefinition;

        public MockPowBlockProvider(PowBlockDefinition blockDefinition)
        {
            this.blockDefinition = blockDefinition;
        }

        public BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script)
        {
            throw new NotImplementedException();
        }

        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            return this.blockDefinition.Build(chainTip, script);
        }
    }

    /// <summary>
    /// A PoW mining fixture that prepares several blocks with a precalculated PoW nonce to save having to recalculate it every unit test.
    /// </summary>
    public class PowMiningTestFixture
    {
        public readonly ConcurrentChain Chain;
        public readonly Key Key;
        public readonly Network Network;

        public Block Block1 { get; set; }
        public ChainedHeader ChainedHeader1 { get; set; }

        public Block Block2 { get; set; }
        public ChainedHeader ChainedHeader2 { get; set; }

        public readonly ReserveScript ReserveScript;

        public PowMiningTestFixture()
        {
            this.Network = KnownNetworks.RegTest; // fast mining so use regtest
            this.Chain = new ConcurrentChain(this.Network);
            this.Key = new Key();
            this.ReserveScript = new ReserveScript(this.Key.ScriptPubKey);

            this.Block1 = this.PrepareValidBlock(this.Chain.Tip, 1, this.Key.ScriptPubKey);
            this.ChainedHeader1 = new ChainedHeader(this.Block1.Header, this.Block1.GetHash(), this.Chain.Tip);

            this.Block2 = this.PrepareValidBlock(this.ChainedHeader1, 2, this.Key.ScriptPubKey);
            this.ChainedHeader2 = new ChainedHeader(this.Block2.Header, this.Block2.GetHash(), this.ChainedHeader1);
        }

        public Block PrepareValidBlock(ChainedHeader prevBlock, int newHeight, Script ScriptPubKey)
        {
            uint nonce = 0;

            Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
            block.Header.HashPrevBlock = prevBlock.HashBlock;

            Transaction transaction = this.Network.CreateTransaction();
            transaction.AddInput(TxIn.CreateCoinbase(newHeight));
            transaction.AddOutput(new TxOut(new Money(1, MoneyUnit.BTC), ScriptPubKey));
            block.Transactions.Add(transaction);

            block.Header.Bits = block.Header.GetWorkRequired(this.Network, prevBlock);
            block.UpdateMerkleRoot();
            while (!block.CheckProofOfWork())
                block.Header.Nonce = ++nonce;

            return block;
        }
    }
}