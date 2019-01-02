using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PosBlockAssemblerTest : LogsTestBase
    {
        private RuleContext callbackRuleContext = null;
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly Mock<IDateTimeProvider> dateTimeProvider;
        private readonly Key key;
        private readonly Mock<ITxMempool> mempool;
        private readonly MinerSettings minerSettings;
        private readonly Network stratisTest;
        private readonly Mock<IStakeChain> stakeChain;
        private readonly Mock<IStakeValidator> stakeValidator;

        public PosBlockAssemblerTest()
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.mempool = new Mock<ITxMempool>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.stakeChain = new Mock<IStakeChain>();
            this.stratisTest = KnownNetworks.StratisTest;
            new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().RegisterRules(this.stratisTest.Consensus);
            this.key = new Key();
            this.minerSettings = new MinerSettings(NodeSettings.Default(this.Network));
            SetupConsensusManager();
        }

        [Fact]
        public void UpdateHeaders_UsingChainAndNetwork_PreparesStakeBlockHeaders()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                this.dateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 1, 7, 0, 0, 0, DateTimeKind.Utc)));

                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, new Key());

                this.stakeValidator.Setup(s => s.GetNextTargetRequired(this.stakeChain.Object, chain.Tip, this.stratisTest.Consensus, true))
                    .Returns(new Target(new uint256(1123123123)))
                    .Verifiable();

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                Block block = posBlockAssembler.UpdateHeaders(chain.Tip);

                Assert.Equal(chain.Tip.HashBlock, block.Header.HashPrevBlock);
                Assert.Equal((uint)1483747200, block.Header.Time);
                Assert.Equal(2.400408204198463E+58, block.Header.Bits.Difficulty);
                Assert.Equal((uint)0, block.Header.Nonce);
                this.stakeValidator.Verify();
            });
        }

        [Fact]
        public void CreateNewBlock_WithScript_ReturnsBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, this.key);
                this.SetupRulesEngine(chain);
                var datetime = new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp()).Returns(datetime.ToUnixTimestamp());
                Transaction transaction = CreateTransaction(this.stratisTest, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                transaction.Time = Utils.DateTimeToUnixTime(datetime);
                var txFee = new Money(1000);

                SetupTxMempool(chain, this.stratisTest.Consensus.Options as PosConsensusOptions, txFee, transaction);

                this.stakeValidator.Setup(s => s.GetNextTargetRequired(this.stakeChain.Object, chain.Tip, this.stratisTest.Consensus, true))
                    .Returns(new Target(new uint256(1123123123)))
                    .Verifiable();

                var posBlockAssembler = new PosBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.mempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.stratisTest, this.stakeChain.Object, this.stakeValidator.Object);
                BlockTemplate blockTemplate = posBlockAssembler.Build(chain.Tip, this.key.ScriptPubKey);

                Assert.Equal(new Money(1000), blockTemplate.TotalFee);
                Assert.Equal(2, blockTemplate.Block.Transactions.Count);
                Assert.Equal(536870912, blockTemplate.Block.Header.Version);

                Assert.Equal(2, blockTemplate.Block.Transactions.Count);

                Transaction resultingTransaction = blockTemplate.Block.Transactions[0];
                Assert.Equal((uint)new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp(), resultingTransaction.Time);
                Assert.NotEmpty(resultingTransaction.Inputs);
                Assert.NotEmpty(resultingTransaction.Outputs);
                Assert.True(resultingTransaction.IsCoinBase);
                Assert.False(resultingTransaction.IsCoinStake);
                Assert.Equal(TxIn.CreateCoinbase(6).ScriptSig, resultingTransaction.Inputs[0].ScriptSig);
                Assert.Equal(new Money(0), resultingTransaction.TotalOut);
                Assert.Equal(new Script(), resultingTransaction.Outputs[0].ScriptPubKey);
                Assert.Equal(new Money(0), resultingTransaction.Outputs[0].Value);

                resultingTransaction = blockTemplate.Block.Transactions[1];
                Assert.NotEmpty(resultingTransaction.Inputs);
                Assert.NotEmpty(resultingTransaction.Outputs);
                Assert.False(resultingTransaction.IsCoinBase);
                Assert.False(resultingTransaction.IsCoinStake);
                Assert.Equal(new Money(400 * 1000 * 1000), resultingTransaction.TotalOut);
                Assert.Equal(transaction.Inputs[0].PrevOut.Hash, resultingTransaction.Inputs[0].PrevOut.Hash);
                Assert.Equal(transaction.Inputs[0].ScriptSig, transaction.Inputs[0].ScriptSig);

                Assert.Equal(transaction.Outputs[0].ScriptPubKey, resultingTransaction.Outputs[0].ScriptPubKey);
                Assert.Equal(new Money(400 * 1000 * 1000), resultingTransaction.Outputs[0].Value);
            });
        }

        [Fact]
        public void CreateNewBlock_WithScript_DoesNotValidateTemplateUsingRuleContext()
        {
            var newOptions = new PosConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, this.key);
                this.SetupRulesEngine(chain);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp()).Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());
                this.consensusManager.Setup(c => c.Tip).Returns(chain.GetBlock(5));

                this.stakeValidator.Setup(s => s.GetNextTargetRequired(this.stakeChain.Object, chain.Tip, this.stratisTest.Consensus, true))
                    .Returns(new Target(new uint256(1123123123)))
                    .Verifiable();

                Transaction transaction = CreateTransaction(this.stratisTest, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);

                SetupTxMempool(chain, newOptions, txFee, transaction);

                var posBlockAssembler = new PosBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.mempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.stratisTest, this.stakeChain.Object, this.stakeValidator.Object);

                BlockTemplate blockTemplate = posBlockAssembler.Build(chain.Tip, this.key.ScriptPubKey);

                this.consensusManager.Verify(c => c.ConsensusRules.PartialValidationAsync(It.IsAny<ChainedHeader>(), It.IsAny<Block>()), Times.Exactly(0));
                Assert.Null(this.callbackRuleContext);
                this.stakeValidator.Verify();
            });
        }

        [Fact]
        public void ComputeBlockVersion_UsingChainTipAndConsensus_NoBip9DeploymentActive_UpdatesHeightAndVersion()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, new Key());
                this.SetupRulesEngine(chain);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings,
                                                 this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                (int Height, int Version) result = posBlockAssembler.ComputeBlockVersion(chain.GetBlock(4));

                Assert.Equal(5, result.Height);
                Assert.Equal((int)ThresholdConditionCache.VersionbitsTopBits, result.Version);
            });
        }

        [Fact]
        public void ComputeBlockVersion_UsingChainTipAndConsensus_Bip9DeploymentActive_UpdatesHeightAndVersion()
        {
            ConsensusOptions options = this.stratisTest.Consensus.Options;
            int minerConfirmationWindow = this.stratisTest.Consensus.MinerConfirmationWindow;
            int ruleChangeActivationThreshold = this.stratisTest.Consensus.RuleChangeActivationThreshold;
            try
            {
                var newOptions = new PosConsensusOptions();
                this.stratisTest.Consensus.Options = newOptions;
                this.stratisTest.Consensus.BIP9Deployments[0] = new BIP9DeploymentsParameters(19,
                    new DateTimeOffset(new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

                this.stratisTest.Consensus.MinerConfirmationWindow = 2;
                this.stratisTest.Consensus.RuleChangeActivationThreshold = 2;

                ConcurrentChain chain = GenerateChainWithHeightAndActivatedBip9(5, this.stratisTest, new Key(), this.stratisTest.Consensus.BIP9Deployments[0]);
                this.SetupRulesEngine(chain);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                (int Height, int Version) result = posBlockAssembler.ComputeBlockVersion(chain.GetBlock(4));

                Assert.Equal(5, result.Height);
                int expectedVersion = (int)(ThresholdConditionCache.VersionbitsTopBits | (((uint)1) << 19));
                Assert.Equal(expectedVersion, result.Version);
                Assert.NotEqual((int)ThresholdConditionCache.VersionbitsTopBits, result.Version);
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                this.stratisTest.Consensus.Options = options;
                this.stratisTest.Consensus.BIP9Deployments[0] = null;
                this.stratisTest.Consensus.MinerConfirmationWindow = minerConfirmationWindow;
                this.stratisTest.Consensus.RuleChangeActivationThreshold = ruleChangeActivationThreshold;
            }
        }

        [Fact]
        public void CreateCoinbase_CreatesCoinbaseTemplateTransaction_AddsToBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                BlockTemplate result = posBlockAssembler.CreateCoinBase(chain.Tip, this.key.ScriptPubKey);

                Assert.NotEmpty(result.Block.Transactions);

                Transaction resultingTransaction = result.Block.Transactions[0];
                Assert.Equal((uint)new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp(), resultingTransaction.Time);
                Assert.True(resultingTransaction.IsCoinBase);
                Assert.False(resultingTransaction.IsCoinStake);
                Assert.Equal(Money.Zero, resultingTransaction.TotalOut);

                Assert.NotEmpty(resultingTransaction.Inputs);
                Assert.Equal(TxIn.CreateCoinbase(6).ScriptSig, resultingTransaction.Inputs[0].ScriptSig);

                Assert.NotEmpty(resultingTransaction.Outputs);
                Assert.Equal(this.key.ScriptPubKey, resultingTransaction.Outputs[0].ScriptPubKey);
                Assert.Equal(Money.Zero, resultingTransaction.Outputs[0].Value);
            });
        }

        [Fact]
        public void AddTransactions_WithoutTransactionsInMempool_DoesNotAddEntriesToBlock()
        {
            var newOptions = new PosConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, new Key());
                this.consensusManager.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
                this.mempool.Setup(t => t.MapTx)
                    .Returns(indexedTransactionSet);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings,
                                                 this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                (Block Block, int Selected, int Updated) result = posBlockAssembler.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionNotInblock_AddsTransactionToBlock()
        {
            var newOptions = new PosConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, this.key);
                this.consensusManager.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));

                Mock<IConsensusRuleEngine> consensusRuleEngine = new Mock<IConsensusRuleEngine>();
                consensusRuleEngine.Setup(s => s.GetRule<PosFutureDriftRule>()).Returns(new PosFutureDriftRule());

                this.consensusManager.Setup(c => c.ConsensusRules)
                    .Returns(consensusRuleEngine.Object);

                Transaction transaction = CreateTransaction(this.stratisTest, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));

                this.dateTimeProvider.Setup(s => s.GetAdjustedTimeAsUnixTimestamp()).Returns(transaction.Time);

                var txFee = new Money(1000);
                SetupTxMempool(chain, newOptions, txFee, transaction);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                posBlockAssembler.CreateCoinBase(new ChainedHeader(this.stratisTest.GetGenesis().Header, this.stratisTest.GetGenesis().Header.GetHash(), 0), new KeyId().ScriptPubKey);

                (Block Block, int Selected, int Updated) result = posBlockAssembler.AddTransactions();

                Assert.NotEmpty(result.Block.Transactions);

                Assert.Equal(transaction.ToHex(), result.Block.Transactions[1].ToHex());
                Assert.Equal(1, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionAlreadyInInblock_DoesNotAddTransactionToBlock()
        {
            var newOptions = new PosConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.stratisTest, this.key);
                this.consensusManager.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                Transaction transaction = CreateTransaction(this.stratisTest, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                TxMempoolEntry[] entries = SetupTxMempool(chain, newOptions, txFee, transaction);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusManager.Object, this.stratisTest, new MempoolSchedulerLock(), this.mempool.Object, this.minerSettings, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);
                posBlockAssembler.AddInBlockTxEntries(entries);

                (Block Block, int Selected, int Updated) result = posBlockAssembler.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        private void ExecuteWithConsensusOptions(PosConsensusOptions newOptions, Action action)
        {
            ConsensusOptions options = this.stratisTest.Consensus.Options;
            try
            {
                this.stratisTest.Consensus.Options = newOptions;

                action();
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                this.stratisTest.Consensus.Options = options;
                this.stratisTest.Consensus.BIP9Deployments[0] = null;
            }
        }

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network, Key key)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                Transaction coinStake = CreateCoinStakeTransaction(network, key, chain.Height + 1, new uint256((ulong)12312312 + (ulong)i));

                block.AddTransaction(coinStake);
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;

                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private static ConcurrentChain GenerateChainWithHeightAndActivatedBip9(int blockAmount, Network network, Key key, BIP9DeploymentsParameters parameter, Target bits = null)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                Transaction coinbase = CreateCoinStakeTransaction(network, key, chain.Height + 1, new uint256((ulong)12312312 + (ulong)i));

                block.AddTransaction(coinbase);
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;

                if (bits != null)
                {
                    block.Header.Bits = bits;
                }

                if (parameter != null)
                {
                    uint version = ThresholdConditionCache.VersionbitsTopBits;
                    version |= ((uint)1) << parameter.Bit;
                    block.Header.Version = (int)version;
                }

                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private static Transaction CreateCoinStakeTransaction(Network network, Key key, int height, uint256 prevout)
        {
            var coinStake = new Transaction();
            coinStake.AddInput(new TxIn(new OutPoint(prevout, 1)));
            coinStake.AddOutput(new TxOut(0, new Script()));
            coinStake.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
            return coinStake;
        }

        private static Transaction CreateTransaction(Network network, Key inkey, int height, Money amount, Key outKey, uint256 prevOutHash)
        {
            var coinbase = new Transaction();
            coinbase.AddInput(new TxIn(new OutPoint(prevOutHash, 1), inkey.ScriptPubKey));
            coinbase.AddOutput(new TxOut(amount, outKey));
            return coinbase;
        }

        private void SetupConsensusManager()
        {
            this.callbackRuleContext = null;
            this.consensusManager.Setup(c => c.ConsensusRules.PartialValidationAsync(It.IsAny<ChainedHeader>(), It.IsAny<Block>()))
                .Callback((ChainedHeader ch, Block b) =>
                {
                    this.callbackRuleContext = new RuleContext();
                });
        }

        private void SetupRulesEngine(ConcurrentChain chain)
        {
            var dateTimeProvider = new DateTimeProvider();

            var posConsensusRules = new PosConsensusRuleEngine(
                this.stratisTest,
                this.LoggerFactory.Object,
                this.dateTimeProvider.Object,
                chain,
                new NodeDeployments(this.stratisTest, chain),
                new ConsensusSettings(new NodeSettings(this.stratisTest)),
                new Checkpoints(),
                new Mock<ICoinView>().Object,
                new Mock<IStakeChain>().Object,
                new Mock<IStakeValidator>().Object,
                new Mock<IChainState>().Object,
                new InvalidBlockHashStore(dateTimeProvider),
                new NodeStats(dateTimeProvider),
                new Mock<IRewindDataIndexCache>().Object);

            posConsensusRules.Register();

            this.consensusManager.SetupGet(x => x.ConsensusRules)
                .Returns(posConsensusRules);
        }

        private TxMempoolEntry[] SetupTxMempool(ConcurrentChain chain, PosConsensusOptions newOptions, Money txFee, params Transaction[] transactions)
        {
            uint txTime = Utils.DateTimeToUnixTime(chain.Tip.Header.BlockTime.AddSeconds(25));
            var lockPoints = new LockPoints()
            {
                Height = 4,
                MaxInputBlock = chain.GetBlock(4),
                Time = chain.GetBlock(4).Header.Time
            };

            var resultingTransactionEntries = new List<TxMempoolEntry>();
            var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
            foreach (Transaction transaction in transactions)
            {
                var txPoolEntry = new TxMempoolEntry(transaction, txFee, txTime, 1, 4, new Money(400000000), false, 2, lockPoints, newOptions);
                indexedTransactionSet.Add(txPoolEntry);
                resultingTransactionEntries.Add(txPoolEntry);
            }


            this.mempool.Setup(t => t.MapTx)
                .Returns(indexedTransactionSet);

            return resultingTransactionEntries.ToArray();
        }

        private class PosTestBlockAssembler : PosBlockDefinition
        {
            public PosTestBlockAssembler(
                IConsensusManager consensusManager,
                Network network,
                MempoolSchedulerLock mempoolLock,
                ITxMempool mempool,
                MinerSettings minerSettings,
                IDateTimeProvider dateTimeProvider,
                IStakeChain stakeChain,
                IStakeValidator stakeValidator,
                ILoggerFactory loggerFactory)
                : base(consensusManager, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network, stakeChain, stakeValidator)
            {
                base.block = this.BlockTemplate.Block;
            }

            public Block UpdateHeaders(ChainedHeader chainTip)
            {
                this.ChainTip = chainTip;
                base.UpdateHeaders();
                return this.block;
            }

            public (int Height, int Version) ComputeBlockVersion(ChainedHeader chainTip)
            {
                base.ChainTip = chainTip;

                base.ComputeBlockVersion();

                return (base.height, base.block.Header.Version);
            }

            public BlockTemplate CreateCoinBase(ChainedHeader chainTip, Script scriptPubKeyIn)
            {
                base.scriptPubKey = scriptPubKeyIn;
                base.ChainTip = chainTip;

                base.CreateCoinbase();

                base.BlockTemplate.Block = base.block;

                return base.BlockTemplate;
            }

            public (Block Block, int Selected, int Updated) AddTransactions()
            {
                base.AddTransactions(out int selected, out int updated);

                return (base.block, selected, updated);
            }

            public void AddInBlockTxEntries(params TxMempoolEntry[] entries)
            {
                foreach (TxMempoolEntry entry in entries)
                {
                    base.inBlock.Add(entry);
                }
            }
        }
    }
}