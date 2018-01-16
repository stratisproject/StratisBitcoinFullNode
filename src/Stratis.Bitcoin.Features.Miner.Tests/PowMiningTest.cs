using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PowMiningTest : LogsTestBase, IClassFixture<PowMiningTestFixture>
    {
        private Mock<IConsensusLoop> consensusLoop;
        private Mock<IAssemblerFactory> assemblerFactory;
        private Mock<INodeLifetime> nodeLifetime;
        private Mock<IAsyncLoopFactory> asyncLoopFactory;
        private Mock<BlockAssembler> blockAssembler;
        private Network network;
        private PowMiningTestFixture fixture;
        private ConcurrentChain chain;
        private PowMining powMining;

        public PowMiningTest(PowMiningTestFixture fixture)
        {
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();

            this.fixture = fixture;
            this.network = Network.StratisTest;
            this.chain = new ConcurrentChain(this.network);

            SetupNodeLifeTime();
            SetupConsensusLoop();
            SetupBlockAssembler();

            this.powMining = new PowMining(this.consensusLoop.Object, this.chain, this.network, this.assemblerFactory.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object, this.LoggerFactory.Object);
        }

        [Fact]
        public void Mine_FirstCall_CreatesNewMiningLoop_ReturnsMiningLoop()
        {
            this.asyncLoopFactory.Setup(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.RunOnce, TimeSpans.TenSeconds))
                .Returns(new AsyncLoop("PowMining.Mine2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; }))
                .Verifiable();

            var result = this.powMining.Mine(new Key().ScriptPubKey);

            Assert.Equal("PowMining.Mine2", result.Name);
            this.nodeLifetime.Verify();
            this.asyncLoopFactory.Verify();
        }

        [Fact]
        public void Mine_SecondCall_ReturnsSameMiningLoop()
        {
            this.asyncLoopFactory.Setup(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.RunOnce, TimeSpans.TenSeconds))
                .Returns(new AsyncLoop("PowMining.Mine2", this.FullNodeLogger.Object, token => { return Task.CompletedTask; }))
                .Verifiable();

            var result = this.powMining.Mine(new Key().ScriptPubKey);
            var result2 = this.powMining.Mine(new Key().ScriptPubKey);

            Assert.Equal("PowMining.Mine2", result.Name);
            Assert.Equal(result, result2);
            this.nodeLifetime.Verify();
            this.asyncLoopFactory.Verify(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.RunOnce, TimeSpans.TenSeconds), Times.Exactly(1));
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

            this.asyncLoopFactory.Setup(a => a.Run("PowMining.Mine", It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>(), TimeSpans.RunOnce, TimeSpans.TenSeconds))
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

            var result = this.powMining.Mine(new Key().ScriptPubKey);

            result.Run(callbackRepeat.Value, null);
        }

        [Fact]
        public void IncrementExtraNonce_HashPrevBlockNotSameAsBlockHeaderHashPrevBlock_ResetsExtraNonceAndHashPrevBlock_UpdatesCoinBaseTransactionAndMerkleRoot()
        {
            FieldInfo hashPrevBlockFieldSelector = GetHashPrevBlockFieldSelector();
            hashPrevBlockFieldSelector.SetValue(this.powMining, new uint256(15));

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());

            var block = new Block();
            block.Transactions.Add(transaction);
            block.Header.HashMerkleRoot = new uint256(0);
            block.Header.HashPrevBlock = new uint256(14);
            this.chain = GenerateChainWithHeight(2, this.network);

            int nExtraNonce = 15;
            nExtraNonce = this.powMining.IncrementExtraNonce(block, this.chain.Tip, nExtraNonce);

            Assert.Equal(new uint256(14), hashPrevBlockFieldSelector.GetValue(this.powMining) as uint256);
            Assert.Equal(block.Transactions[0].Inputs[0].ScriptSig, TxIn.CreateCoinbase(3).ScriptSig);
            Assert.NotEqual(new uint256(0), block.Header.HashMerkleRoot);
            Assert.Equal(1, nExtraNonce);
        }

        [Fact]
        public void IncrementExtraNonce_HashPrevBlockNotSameAsBlockHeaderHashPrevBlock_IncrementsExtraNonce_UpdatesCoinBaseTransactionAndMerkleRoot()
        {
            FieldInfo hashPrevBlockFieldSelector = GetHashPrevBlockFieldSelector();
            hashPrevBlockFieldSelector.SetValue(this.powMining, new uint256(15));

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn());

            var block = new Block();
            block.Transactions.Add(transaction);
            block.Header.HashMerkleRoot = new uint256(0);
            block.Header.HashPrevBlock = new uint256(15);
            this.chain = GenerateChainWithHeight(2, this.network);

            int nExtraNonce = 15;
            nExtraNonce = this.powMining.IncrementExtraNonce(block, this.chain.Tip, nExtraNonce);

            Assert.Equal(block.Transactions[0].Inputs[0].ScriptSig, TxIn.CreateCoinbase(3).ScriptSig);
            Assert.NotEqual(new uint256(0), block.Header.HashMerkleRoot);
            Assert.Equal(16, nExtraNonce);
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_ReturnsGeneratedBlock()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                BlockValidationContext callbackBlockValidationContext = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        context.ChainedBlock = new ChainedBlock(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                        this.chain.SetTip(context.ChainedBlock);
                        callbackBlockValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                this.blockAssembler.Setup(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 1, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackBlockValidationContext.Block.GetHash(), blockHashes[0]);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_ChainedBlockNotPresentInBlockValidationContext_ReturnsEmptyList()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                BlockValidationContext callbackBlockValidationContext = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        context.ChainedBlock = null;
                        callbackBlockValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                this.blockAssembler.Setup(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 1, uint.MaxValue);

                Assert.Empty(blockHashes);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_ValidationContextError_ReturnsEmptyList()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                BlockValidationContext callbackBlockValidationContext = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        context.ChainedBlock = new ChainedBlock(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                        this.chain.SetTip(context.ChainedBlock);
                        context.Error = ConsensusErrors.BadMerkleRoot;
                        callbackBlockValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                this.blockAssembler.Setup(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 1, uint.MaxValue);

                Assert.Empty(blockHashes);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_BlockValidationContextErrorInvalidPrevTip_ContinuesExecution_ReturnsGeneratedBlock()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                BlockValidationContext callbackBlockValidationContext = null;
                ConsensusError lastError = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        context.ChainedBlock = new ChainedBlock(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                        if (lastError == null)
                        {
                            context.Error = ConsensusErrors.InvalidPrevTip;
                            lastError = context.Error;
                        }
                        else if (lastError != null)
                        {
                            this.chain.SetTip(context.ChainedBlock);
                        }
                        callbackBlockValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                this.blockAssembler.Setup(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 1, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackBlockValidationContext.Block.GetHash(), blockHashes[0]);
            });
        }

        [Fact]
        public void GenerateBlocks_SingleBlock_MaxTriesReached_StopsGeneratingBlocks_ReturnsEmptyList()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                BlockValidationContext callbackBlockValidationContext = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        context.ChainedBlock = new ChainedBlock(context.Block.Header, context.Block.GetHash(), this.chain.Tip);
                        this.chain.SetTip(context.ChainedBlock);
                        callbackBlockValidationContext = context;
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                blockTemplate.Block.Header.Nonce = 0;
                this.blockAssembler.Setup(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 1, 15);

                Assert.Empty(blockHashes);
            });
        }

        [Fact]
        public void GenerateBlocks_ZeroBlocks_ReturnsEmptyList()
        {
            var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 0, int.MaxValue);

            Assert.Empty(blockHashes);
        }

        [Fact]
        public void GenerateBlocks_MultipleBlocks_ReturnsGeneratedBlocks()
        {
            this.ExecuteUsingNonProofOfStakeSettings(() =>
            {
                List<BlockValidationContext> callbackBlockValidationContexts = new List<BlockValidationContext>();
                ChainedBlock lastChainedBlock = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        if (lastChainedBlock == null)
                        {
                            context.ChainedBlock = this.fixture.chainedBlock1;
                            lastChainedBlock = context.ChainedBlock;
                        }
                        else
                        {
                            context.ChainedBlock = this.fixture.chainedBlock2;
                        }

                        this.chain.SetTip(context.ChainedBlock);
                        callbackBlockValidationContexts.Add(context);
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                BlockTemplate blockTemplate2 = CreateBlockTemplate(this.fixture.block2);

                int attempts = 0;
                this.blockAssembler.Setup(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(() =>
                    {
                        if (lastChainedBlock == null)
                        {
                            if (attempts == 10)
                            {
                                // sometimes the PoW nonce we generate in the fixture is not accepted resulting in an infinite loop. Retry.
                                this.fixture.block1 = this.fixture.PrepareValidBlock(this.chain.Tip, 1, this.fixture.key.ScriptPubKey);
                                this.fixture.chainedBlock1 = new ChainedBlock(this.fixture.block1.Header, this.fixture.block1.GetHash(), this.chain.Tip);
                                this.fixture.block2 = this.fixture.PrepareValidBlock(this.fixture.chainedBlock1, 2, this.fixture.key.ScriptPubKey);
                                this.fixture.chainedBlock2 = new ChainedBlock(this.fixture.block2.Header, this.fixture.block2.GetHash(), this.fixture.chainedBlock1);

                                blockTemplate = CreateBlockTemplate(this.fixture.block1);
                                blockTemplate2 = CreateBlockTemplate(this.fixture.block2);
                                attempts = 0;
                            }
                            attempts += 1;

                            return blockTemplate;
                        }

                        return blockTemplate2;
                    });

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 2, uint.MaxValue);

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
                List<BlockValidationContext> callbackBlockValidationContexts = new List<BlockValidationContext>();
                ChainedBlock lastChainedBlock = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        if (lastChainedBlock == null)
                        {
                            context.ChainedBlock = this.fixture.chainedBlock1;
                            lastChainedBlock = context.ChainedBlock;
                            this.chain.SetTip(context.ChainedBlock);
                        }
                        else
                        {
                            context.ChainedBlock = null;
                        }

                        callbackBlockValidationContexts.Add(context);
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                BlockTemplate blockTemplate2 = CreateBlockTemplate(this.fixture.block2);

                this.blockAssembler.SetupSequence(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate)
                    .Returns(blockTemplate2);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 2, uint.MaxValue);

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
                List<BlockValidationContext> callbackBlockValidationContexts = new List<BlockValidationContext>();
                ChainedBlock lastChainedBlock = null;
                this.consensusLoop.Setup(c => c.AcceptBlockAsync(It.IsAny<BlockValidationContext>()))
                    .Callback<BlockValidationContext>((context) =>
                    {
                        if (lastChainedBlock == null)
                        {
                            context.ChainedBlock = this.fixture.chainedBlock1;
                            this.chain.SetTip(context.ChainedBlock);
                            lastChainedBlock = context.ChainedBlock;
                        }
                        else
                        {
                            context.Error = ConsensusErrors.BadBlockLength;
                        }

                        callbackBlockValidationContexts.Add(context);
                    })
                    .Returns(Task.CompletedTask);

                BlockTemplate blockTemplate = CreateBlockTemplate(this.fixture.block1);
                BlockTemplate blockTemplate2 = CreateBlockTemplate(this.fixture.block2);

                this.blockAssembler.SetupSequence(b => b.CreateNewBlock(It.Is<Script>(r => r == this.fixture.reserveScript.ReserveFullNodeScript), true))
                    .Returns(blockTemplate)
                    .Returns(blockTemplate2);

                var blockHashes = this.powMining.GenerateBlocks(this.fixture.reserveScript, 2, uint.MaxValue);

                Assert.NotEmpty(blockHashes);
                Assert.True(blockHashes.Count == 1);
                Assert.Equal(callbackBlockValidationContexts[0].Block.GetHash(), blockHashes[0]);
            });
        }

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
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
            var typeToTest = typeof(PowMining);
            var hashPrevBlockFieldSelector = typeToTest.GetField("hashPrevBlock", BindingFlags.Instance | BindingFlags.NonPublic);
            return hashPrevBlockFieldSelector;
        }

        private void SetupConsensusLoop()
        {
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(() =>
                {
                    return this.chain.Tip;
                });
        }

        private void SetupNodeLifeTime()
        {
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.nodeLifetime.Setup(n => n.ApplicationStopping)
               .Returns(new CancellationToken())
               .Verifiable();
        }

        private void SetupBlockAssembler()
        {
            this.assemblerFactory = new Mock<IAssemblerFactory>();
            this.blockAssembler = new Mock<BlockAssembler>();
            this.assemblerFactory.Setup(a => a.Create(It.IsAny<ChainedBlock>(), null))
                .Returns(this.blockAssembler.Object);
        }

        private BlockTemplate CreateBlockTemplate(Block block)
        {
            BlockTemplate blockTemplate = new BlockTemplate();
            blockTemplate.Block = new Block(block.Header);
            blockTemplate.Block.Transactions = block.Transactions;
            return blockTemplate;
        }

        private void ExecuteUsingNonProofOfStakeSettings(Action action)
        {
            var isProofOfStake = this.network.NetworkOptions.IsProofOfStake;
            var blockSignature = Block.BlockSignature;
            var timestamp = Transaction.TimeStamp;

            try
            {
                this.network.NetworkOptions.IsProofOfStake = false;
                Block.BlockSignature = false;
                Transaction.TimeStamp = false;

                action();
            }
            finally
            {
                this.network.NetworkOptions.IsProofOfStake = isProofOfStake;
                Block.BlockSignature = blockSignature;
                Transaction.TimeStamp = timestamp;
            }
        }
    }

    /// <summary>
    /// A PoW mining fixture that prepares several blocks with a precalculated PoW nonce to save having to recalculate it every unit test.
    /// </summary>
    public class PowMiningTestFixture
    {
        private Network network;
        private ConcurrentChain chain;
        public Key key;
        public Block block1;
        public ChainedBlock chainedBlock1;
        public Block block2;
        public ChainedBlock chainedBlock2;
        public ReserveScript reserveScript;

        public PowMiningTestFixture()
        {
            this.network = Network.StratisTest;
            this.chain = new ConcurrentChain(this.network);
            this.key = new Key();
            this.reserveScript = new ReserveScript(this.key.ScriptPubKey);

            var isProofOfStake = this.network.NetworkOptions.IsProofOfStake;
            var blockSignature = Block.BlockSignature;
            var timestamp = Transaction.TimeStamp;

            try
            {
                this.network.NetworkOptions.IsProofOfStake = false;
                Block.BlockSignature = false;
                Transaction.TimeStamp = false;
                this.block1 = PrepareValidBlock(this.chain.Tip, 1, this.key.ScriptPubKey);
                this.chainedBlock1 = new ChainedBlock(this.block1.Header, this.block1.GetHash(), this.chain.Tip);
                this.block2 = PrepareValidBlock(this.chainedBlock1, 2, this.key.ScriptPubKey);
                this.chainedBlock2 = new ChainedBlock(this.block2.Header, this.block2.GetHash(), this.chainedBlock1);
            }
            finally
            {
                this.network.NetworkOptions.IsProofOfStake = isProofOfStake;
                Block.BlockSignature = blockSignature;
                Transaction.TimeStamp = timestamp;
            }
        }

        public Block PrepareValidBlock(ChainedBlock prevBlock, int newHeight, Script ScriptPubKey)
        {
            uint nonce = 0;

            var block = new Block();
            block.Header.HashPrevBlock = prevBlock.HashBlock;

            var transaction = new Transaction();
            transaction.AddInput(TxIn.CreateCoinbase(newHeight));
            transaction.AddOutput(new TxOut(new Money(1, MoneyUnit.BTC), ScriptPubKey));
            block.Transactions.Add(transaction);

            block.Header.Bits = block.Header.GetWorkRequired(this.network, prevBlock);
            block.UpdateMerkleRoot();
            while (!block.CheckProofOfWork(this.network.Consensus))
                block.Header.Nonce = ++nonce;

            return block;
        }
    }
}
