using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PowMiningTest : LogsTestBase, IClassFixture<PowMiningTestFixture>
    {
        private Mock<IAsyncLoopFactory> asyncLoopFactory;
        private ConcurrentChain chain;
        private Mock<IConsensusLoop> consensusLoop;
        private Mock<IConsensusRules> consensusRules;
        private NBitcoin.Consensus.ConsensusOptions initialNetworkOptions;
        private PowMiningTestFixture fixture;
        private Mock<ITxMempool> mempool;
        private MempoolSchedulerLock mempoolLock;
        private Network network;
        private Mock<INodeLifetime> nodeLifetime;

        public PowMiningTest(PowMiningTestFixture fixture)
        {
            this.fixture = fixture;
            this.network = fixture.Network;

            this.initialNetworkOptions = this.network.Consensus.Options;
            if (this.initialNetworkOptions == null)
                this.network.Consensus.Options = new PowConsensusOptions();

            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();

            this.consensusLoop = new Mock<IConsensusLoop>();
            this.consensusLoop.SetupGet(c => c.Tip).Returns(() => this.chain.Tip);

            this.mempool = new Mock<ITxMempool>();
            this.mempool.SetupGet(mp => mp.MapTx).Returns(new TxMempool.IndexedTransactionSet());

            this.chain = fixture.Chain;

            this.nodeLifetime = new Mock<INodeLifetime>();
            this.nodeLifetime.Setup(n => n.ApplicationStopping).Returns(new CancellationToken()).Verifiable();

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
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                ValidationContext callbackValidationContext = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>()))
                    .Callback<ValidationContext>((context) =>
                    {
                        context.ChainedHeader = new ChainedHeader(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                        this.chain.SetTip(context.ChainedHeader);
                        callbackValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
                blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

                PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackValidationContext.Block.GetHash(), blockHashes[0]);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_ChainedBlockNotPresentInBlockValidationContext_ReturnsEmptyList()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                ValidationContext callbackValidationContext = null;

                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>()))
                    .Callback<ValidationContext>((context) =>
                    {
                        context.ChainedHeader = null;
                        callbackValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
                blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

                PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, uint.MaxValue);

                Assert.Empty(blockHashes);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_ValidationContextError_ReturnsEmptyList()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                ValidationContext callbackValidationContext = null;

                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>()))
                    .Callback<ValidationContext>((context) =>
                    {
                        context.ChainedHeader = new ChainedHeader(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                        this.chain.SetTip(context.ChainedHeader);
                        context.Error = ConsensusErrors.BadMerkleRoot;
                        callbackValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
                blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

                PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, uint.MaxValue);

                Assert.Empty(blockHashes);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_BlockValidationContextErrorInvalidPrevTip_ContinuesExecution_ReturnsGeneratedBlock()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                ValidationContext callbackValidationContext = null;

                ConsensusError lastError = null;

                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>())).Callback<ValidationContext>((context) =>
                {
                    context.ChainedHeader = new ChainedHeader(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                    if (lastError == null)
                    {
                        context.Error = ConsensusErrors.InvalidPrevTip;
                        lastError = context.Error;
                    }
                    else if (lastError != null)
                    {
                        this.chain.SetTip(context.ChainedHeader);
                    }
                    callbackValidationContext = context;
                }).Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
                blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

                PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackValidationContext.Block.GetHash(), blockHashes[0]);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_MaxTriesReached_StopsGeneratingBlocks_ReturnsEmptyList()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                ValidationContext callbackValidationContext = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>())).Callback<ValidationContext>((context) =>
                {
                    context.ChainedHeader = new ChainedHeader(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                    this.chain.SetTip(context.ChainedHeader);
                    callbackValidationContext = context;
                }).Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);
                blockTemplate.Block.Header.Nonce = 0;
                blockTemplate.Block.Header.Bits = Network.TestNet.GetGenesis().Header.Bits; // make the difficulty harder.

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
                blockBuilder.Setup(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript))).Returns(blockTemplate);

                PowMining miner = CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 1, 15);

                Assert.Empty(blockHashes);
            });
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
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                var callbackBlockValidationContexts = new List<ValidationContext>();
                ChainedHeader lastChainedHeader = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>()))
                    .Callback<ValidationContext>((context) =>
                    {
                        if (lastChainedHeader == null)
                        {
                            context.ChainedHeader = this.fixture.ChainedHeader1;
                            lastChainedHeader = context.ChainedHeader;
                        }
                        else
                        {
                            context.ChainedHeader = this.fixture.ChainedHeader2;
                        }

                        this.chain.SetTip(context.ChainedHeader);
                        callbackBlockValidationContexts.Add(context);
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);
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
                Assert.Equal(callbackBlockValidationContexts[0].Block.GetHash(), blockHashes[0]);
                Assert.Equal(callbackBlockValidationContexts[1].Block.GetHash(), blockHashes[1]);
            });
        }

        [Fact]
        public void GenerateBlocks_MultipleBlocks_ChainedBlockNotPresentInBlockValidationContext_ReturnsValidGeneratedBlocks()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                var callbackBlockValidationContexts = new List<ValidationContext>();
                ChainedHeader lastChainedHeader = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>()))
                    .Callback<ValidationContext>((context) =>
                    {
                        if (lastChainedHeader == null)
                        {
                            context.ChainedHeader = this.fixture.ChainedHeader1;
                            lastChainedHeader = context.ChainedHeader;
                            this.chain.SetTip(context.ChainedHeader);
                        }
                        else
                        {
                            context.ChainedHeader = null;
                        }

                        callbackBlockValidationContexts.Add(context);
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);
                BlockTemplate blockTemplate2 = this.CreateBlockTemplate(this.fixture.Block2);

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();
                blockBuilder.SetupSequence(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript)))
                    .Returns(blockTemplate)
                    .Returns(blockTemplate2);

                PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 2, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackBlockValidationContexts[0].Block.GetHash(), blockHashes[0]);
            });
        }

        [Fact]
        public void GenerateBlocks_MultipleBlocks_BlockValidationContextError_ReturnsValidGeneratedBlocks()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                var callbackBlockValidationContexts = new List<ValidationContext>();

                ChainedHeader lastChainedBlock = null;

                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<ValidationContext>())).Callback<ValidationContext>((context) =>
                {
                    if (lastChainedBlock == null)
                    {
                        context.ChainedHeader = this.fixture.ChainedHeader1;
                        this.chain.SetTip(context.ChainedHeader);
                        lastChainedBlock = context.ChainedHeader;
                    }
                    else
                    {
                        context.Error = ConsensusErrors.BadBlockLength;
                    }

                    callbackBlockValidationContexts.Add(context);
                })
                .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = this.CreateBlockTemplate(this.fixture.Block1);
                BlockTemplate blockTemplate2 = this.CreateBlockTemplate(this.fixture.Block2);

                this.chain.SetTip(this.chain.GetBlock(0));

                Mock<PowBlockDefinition> blockBuilder = this.CreateProofOfWorkBlockBuilder();

                blockBuilder.SetupSequence(b => b.Build(It.IsAny<ChainedHeader>(), It.Is<Script>(r => r == this.fixture.ReserveScript.ReserveFullNodeScript)))
                            .Returns(blockTemplate)
                            .Returns(blockTemplate2);

                PowMining miner = this.CreateProofOfWorkMiner(blockBuilder.Object);
                List<uint256> blockHashes = miner.GenerateBlocks(this.fixture.ReserveScript, 2, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackBlockValidationContexts[0].Block.GetHash(), blockHashes[0]);
            });
        }

        private Mock<PowBlockDefinition> CreateProofOfWorkBlockBuilder()
        {
            return new Mock<PowBlockDefinition>(
                    this.consensusLoop.Object,
                    DateTimeProvider.Default,
                    this.LoggerFactory.Object,
                    this.mempool.Object,
                    this.mempoolLock,
                    this.network,
                    this.consensusRules, 
                    null);
        }

        private PowMining CreateProofOfWorkMiner(PowBlockDefinition blockDefinition)
        {
            var blockBuilder = new MockPowBlockProvider(blockDefinition);
            return new PowMining(this.asyncLoopFactory.Object, blockBuilder, this.consensusLoop.Object, this.chain, DateTimeProvider.Default, this.mempool.Object, this.mempoolLock, this.network, this.nodeLifetime.Object, this.LoggerFactory.Object);
        }

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.Consensus.ConsensusFactory.CreateTransaction());
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

        private void ExecuteUsingNonProofOfStakeSettings(Action action)
        {
            action();
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
            this.Network = Network.RegTest; // fast mining so use regtest
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

            Transaction transaction = this.Network.Consensus.ConsensusFactory.CreateTransaction();
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