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
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PowBlockAssemblerTest : LogsTestBase
    {
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly Mock<IConsensusRuleEngine> consensusRules;

        private readonly Mock<ITxMempool> txMempool;
        private readonly Mock<IDateTimeProvider> dateTimeProvider;
        private RuleContext callbackRuleContext;
        private readonly Money powReward;
        private readonly MinerSettings minerSettings;
        private readonly Network testNet;
        private readonly Key key;

        public PowBlockAssemblerTest()
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.consensusRules = new Mock<IConsensusRuleEngine>();
            this.txMempool = new Mock<ITxMempool>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.powReward = Money.Coins(50);
            this.testNet = KnownNetworks.TestNet;
            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(this.testNet.Consensus);
            this.minerSettings = new MinerSettings(NodeSettings.Default(this.Network));
            this.key = new Key();

            this.SetupConsensusManager();
        }

        [Fact]
        public void CreateNewBlock_WithScript_ReturnsBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new ConsensusOptions(), () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, this.key);
                this.SetupRulesEngine(chain);
                this.consensusManager.Setup(s => s.Tip).Returns(chain.Tip);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());
                Transaction transaction = CreateTransaction(this.testNet, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                SetupTxMempool(chain, this.testNet.Consensus.Options as ConsensusOptions, txFee, transaction);
                this.consensusRules
                    .Setup(s => s.CreateRuleContext(It.IsAny<ValidationContext>()))
                    .Returns(new PowRuleContext());

                var blockDefinition = new PowBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);

                BlockTemplate blockTemplate = blockDefinition.Build(chain.Tip, this.key.ScriptPubKey);

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
                Assert.Equal(this.powReward + txFee, resultingTransaction.TotalOut);
                Assert.Equal(this.key.ScriptPubKey, resultingTransaction.Outputs[0].ScriptPubKey);
                Assert.Equal(this.powReward + txFee, resultingTransaction.Outputs[0].Value);

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
        public void ComputeBlockVersion_UsingChainTipAndConsensus_NoBip9DeploymentActive_UpdatesHeightAndVersion()
        {
            this.ExecuteWithConsensusOptions(new ConsensusOptions(), () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, new Key());
                this.SetupRulesEngine(chain);

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);

                (int Height, int Version) result = blockDefinition.ComputeBlockVersion(chain.GetBlock(4));

                Assert.Equal(5, result.Height);
                Assert.Equal((int)ThresholdConditionCache.VersionbitsTopBits, result.Version);
            });
        }

        [Fact]
        public void ComputeBlockVersion_UsingChainTipAndConsensus_Bip9DeploymentActive_UpdatesHeightAndVersion()
        {
            ConsensusOptions options = this.testNet.Consensus.Options;
            int minerConfirmationWindow = this.testNet.Consensus.MinerConfirmationWindow;
            int ruleChangeActivationThreshold = this.testNet.Consensus.RuleChangeActivationThreshold;
            try
            {
                var newOptions = new ConsensusOptions();
                this.testNet.Consensus.Options = newOptions;
                this.testNet.Consensus.BIP9Deployments[0] = new BIP9DeploymentsParameters(19,
                    new DateTimeOffset(new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

                // As we are effectively using TestNet the other deployments need to be disabled
                this.testNet.Consensus.BIP9Deployments[BitcoinBIP9Deployments.CSV] = null;
                this.testNet.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit] = null;

                this.testNet.Consensus.MinerConfirmationWindow = 2;
                this.testNet.Consensus.RuleChangeActivationThreshold = 2;

                ConcurrentChain chain = GenerateChainWithHeightAndActivatedBip9(5, this.testNet, new Key(), this.testNet.Consensus.BIP9Deployments[0]);
                this.SetupRulesEngine(chain);

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);

                (int Height, int Version) result = blockDefinition.ComputeBlockVersion(chain.GetBlock(4));

                Assert.Equal(5, result.Height);
                int expectedVersion = (int)(ThresholdConditionCache.VersionbitsTopBits | (((uint)1) << 19));
                Assert.Equal(expectedVersion, result.Version);
                Assert.NotEqual((int)ThresholdConditionCache.VersionbitsTopBits, result.Version);
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                this.testNet.Consensus.Options = options;
                this.testNet.Consensus.BIP9Deployments[0] = null;
                this.testNet.Consensus.MinerConfirmationWindow = minerConfirmationWindow;
                this.testNet.Consensus.RuleChangeActivationThreshold = ruleChangeActivationThreshold;
            }
        }

        [Fact]
        public void CreateCoinbase_CreatesCoinbaseTemplateTransaction_AddsToBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new ConsensusOptions(), () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);

                BlockTemplate result = blockDefinition.CreateCoinBase(chain.Tip, this.key.ScriptPubKey);

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
        public void UpdateHeaders_UsingChainAndNetwork_PreparesBlockHeaders()
        {
            this.ExecuteWithConsensusOptions(new ConsensusOptions(), () =>
            {
                this.dateTimeProvider.Setup(d => d.GetTimeOffset()).Returns(new DateTimeOffset(new DateTime(2017, 1, 7, 0, 0, 0, DateTimeKind.Utc)));

                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, new Key(), new Target(235325239));

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);
                Block block = blockDefinition.UpdateHeaders(chain.Tip);

                Assert.Equal(chain.Tip.HashBlock, block.Header.HashPrevBlock);
                Assert.Equal((uint)1483747200, block.Header.Time);
                Assert.Equal(1, block.Header.Bits.Difficulty);
                Assert.Equal((uint)0, block.Header.Nonce);
            });
        }

        [Fact]
        public void AddTransactions_WithoutTransactionsInMempool_DoesNotAddEntriesToBlock()
        {
            var newOptions = new ConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, new Key());
                this.consensusManager.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
                this.txMempool.Setup(t => t.MapTx)
                    .Returns(indexedTransactionSet);

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);

                (Block Block, int Selected, int Updated) result = blockDefinition.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionNotInblock_AddsTransactionToBlock()
        {
            var newOptions = new ConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, this.key);
                this.consensusManager.Setup(c => c.Tip).Returns(chain.GetBlock(5));
                Transaction transaction = CreateTransaction(this.testNet, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                SetupTxMempool(chain, newOptions, txFee, transaction);

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);

                (Block Block, int Selected, int Updated) result = blockDefinition.AddTransactions();

                Assert.NotEmpty(result.Block.Transactions);

                Assert.Equal(transaction.ToHex(), result.Block.Transactions[0].ToHex());
                Assert.Equal(1, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionAlreadyInInblock_DoesNotAddTransactionToBlock()
        {
            var newOptions = new ConsensusOptions();

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                ConcurrentChain chain = GenerateChainWithHeight(5, this.testNet, this.key);
                this.consensusManager.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                Transaction transaction = CreateTransaction(this.testNet, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                TxMempoolEntry[] entries = SetupTxMempool(chain, newOptions, txFee, transaction);

                var blockDefinition = new PowTestBlockDefinition(this.consensusManager.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.minerSettings, this.testNet, this.consensusRules.Object);
                blockDefinition.AddInBlockTxEntries(entries);

                (Block Block, int Selected, int Updated) result = blockDefinition.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        private void ExecuteWithConsensusOptions(ConsensusOptions newOptions, Action action)
        {
            ConsensusOptions options = this.testNet.Consensus.Options;
            try
            {
                this.testNet.Consensus.Options = newOptions;

                action();
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                this.testNet.Consensus.Options = options;
                this.testNet.Consensus.BIP9Deployments[0] = null;
            }
        }

        private static ConcurrentChain GenerateChainWithHeightAndActivatedBip9(int blockAmount, Network network, Key key, BIP9DeploymentsParameters parameter, Target bits = null)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                Transaction coinbase = CreateCoinbaseTransaction(network, key, chain.Height + 1);

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

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network, Key key, Target bits = null)
        {
            return GenerateChainWithHeightAndActivatedBip9(blockAmount, network, key, null, bits);
        }

        private static Transaction CreateTransaction(Network network, Key inkey, int height, Money amount, Key outKey, uint256 prevOutHash)
        {
            var coinbase = new Transaction();
            coinbase.AddInput(new TxIn(new OutPoint(prevOutHash, 1), inkey.ScriptPubKey));
            coinbase.AddOutput(new TxOut(amount, outKey));
            return coinbase;
        }

        private static Transaction CreateCoinbaseTransaction(Network network, Key key, int height)
        {
            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(height));
            coinbase.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
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

            var powConsensusRules = new PowConsensusRuleEngine(this.testNet,
                    this.LoggerFactory.Object, this.dateTimeProvider.Object, chain,
                    new NodeDeployments(this.testNet, chain), new ConsensusSettings(new NodeSettings(this.testNet)), new Checkpoints(),
                    new Mock<ICoinView>().Object, new Mock<IChainState>().Object, new InvalidBlockHashStore(dateTimeProvider), new NodeStats(dateTimeProvider));

            powConsensusRules.Register();
            this.consensusManager.SetupGet(x => x.ConsensusRules).Returns(powConsensusRules);
        }

        private TxMempoolEntry[] SetupTxMempool(ConcurrentChain chain, ConsensusOptions newOptions, Money txFee, params Transaction[] transactions)
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


            this.txMempool.Setup(t => t.MapTx)
                .Returns(indexedTransactionSet);

            return resultingTransactionEntries.ToArray();
        }

        private class PowTestBlockDefinition : PowBlockDefinition
        {
            public PowTestBlockDefinition(
                IConsensusManager consensusLoop,
                IDateTimeProvider dateTimeProvider,
                ILoggerFactory loggerFactory,
                ITxMempool mempool,
                MempoolSchedulerLock mempoolLock,
                MinerSettings minerSettings,
                Network network,
                IConsensusRuleEngine consensusRules,
                BlockDefinitionOptions options = null)
                : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network, consensusRules)
            {
                this.block = this.BlockTemplate.Block;
            }

            public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
            {
                OnBuild(chainTip, scriptPubKey);

                return this.BlockTemplate;
            }

            public void AddInBlockTxEntries(params TxMempoolEntry[] entries)
            {
                foreach (TxMempoolEntry entry in entries)
                {
                    this.inBlock.Add(entry);
                }
            }

            public (int Height, int Version) ComputeBlockVersion(ChainedHeader chainTip)
            {
                this.ChainTip = chainTip;

                base.ComputeBlockVersion();
                return (this.height, this.block.Header.Version);
            }

            public BlockTemplate CreateCoinBase(ChainedHeader chainTip, Script scriptPubKeyIn)
            {
                this.scriptPubKey = scriptPubKeyIn;
                this.ChainTip = chainTip;
                base.CreateCoinbase();
                this.BlockTemplate.Block = this.block;

                return this.BlockTemplate;
            }

            public Block UpdateHeaders(ChainedHeader chainTip)
            {
                this.ChainTip = chainTip;
                base.UpdateHeaders();
                return this.BlockTemplate.Block;
            }

            public (Block Block, int Selected, int Updated) AddTransactions()
            {
                int selected;
                int updated;
                base.AddTransactions(out selected, out updated);

                return (this.block, selected, updated);
            }
        }
    }
}