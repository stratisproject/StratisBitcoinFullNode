using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class PowBlockAssemblerTest : LogsTestBase
    {
        private Mock<IConsensusLoop> consensusLoop;
        private Mock<ITxMempool> txMempool;
        private Mock<IDateTimeProvider> dateTimeProvider;
        private Mock<IPowConsensusValidator> validator;
        private RuleContext callbackRuleContext = null;
        private Money powReward;
        private Network network;
        private Key key;

        public PowBlockAssemblerTest()
        {
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.txMempool = new Mock<ITxMempool>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.validator = new Mock<IPowConsensusValidator>();
            this.powReward = new Money(100 * 1000 * 1000);
            this.network = Network.StratisTest;
            this.key = new Key();

            SetupValidator();
            SetupConsensusLoop();

            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
        }

        [Fact]
        public void CreateNewBlock_WithScript_ReturnsBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new PowConsensusOptions(), () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());
                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                SetupTxMempool(chain, this.network.Consensus.Options as PowConsensusOptions, txFee, transaction);

                var powBlockAssembler = new PowBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.network);

                var blockTemplate = powBlockAssembler.Build(chain.Tip, this.key.ScriptPubKey);

                Assert.Null(blockTemplate.CoinbaseCommitment);
                Assert.Equal(new Money(1000), blockTemplate.TotalFee);
                Assert.Equal(2, blockTemplate.TxSigOpsCost.Count);
                Assert.Equal(-1, blockTemplate.TxSigOpsCost[0]);
                Assert.Equal(2, blockTemplate.TxSigOpsCost[1]);
                Assert.Equal(2, blockTemplate.VTxFees.Count);
                Assert.Equal(new Money(-1000), blockTemplate.VTxFees[0]);
                Assert.Equal(new Money(1000), blockTemplate.VTxFees[1]);
                Assert.Equal(2, blockTemplate.Block.Transactions.Count);
                Assert.Equal(536870912, blockTemplate.Block.Header.Version);

                Assert.Equal(2, blockTemplate.Block.Transactions.Count);

                var resultingTransaction = blockTemplate.Block.Transactions[0];
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
        public void CreateNewBlock_WithScript_ValidatesTemplateUsingRuleContext()
        {
            var newOptions = new PowConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));

                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                SetupTxMempool(chain, this.network.Consensus.Options as PowConsensusOptions, txFee, transaction);

                var powBlockAssembler = new PowBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.network);

                var blockTemplate = powBlockAssembler.Build(chain.Tip, this.key.ScriptPubKey);
                Assert.NotNull(this.callbackRuleContext);

                Assert.False(this.callbackRuleContext.CheckMerkleRoot);
                Assert.False(this.callbackRuleContext.CheckPow);
                Assert.Equal(blockTemplate.Block.GetHash(), this.callbackRuleContext.BlockValidationContext.Block.GetHash());
                Assert.Equal(chain.GetBlock(5).HashBlock, this.callbackRuleContext.ConsensusTip.HashBlock);
                Assert.Equal(1500, this.callbackRuleContext.Consensus.Option<PowConsensusOptions>().MaxBlockWeight);
                this.consensusLoop.Verify();
            });
        }

        [Fact]
        public void ComputeBlockVersion_UsingChainTipAndConsensus_NoBip9DeploymentActive_UpdatesHeightAndVersion()
        {
            this.ExecuteWithConsensusOptions(new PowConsensusOptions(), () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, new Key());

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.network);

                var result = powBlockAssembler.ComputeBlockVersion(chain.GetBlock(4));

                Assert.Equal(5, result.Height);
                uint version = ThresholdConditionCache.VersionbitsTopBits;
                Assert.Equal((int)version, result.Version);
            });
        }


        [Fact]
        public void ComputeBlockVersion_UsingChainTipAndConsensus_Bip9DeploymentActive_UpdatesHeightAndVersion()
        {
            var options = this.network.Consensus.Options;
            var minerConfirmationWindow = this.network.Consensus.MinerConfirmationWindow;
            var ruleChangeActivationThreshold = this.network.Consensus.RuleChangeActivationThreshold;
            try
            {
                var newOptions = new PowConsensusOptions();
                this.network.Consensus.Options = newOptions;
                this.network.Consensus.BIP9Deployments[0] = new BIP9DeploymentsParameters(19,
                    new DateTimeOffset(new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
                this.network.Consensus.MinerConfirmationWindow = 2;
                this.network.Consensus.RuleChangeActivationThreshold = 2;

                var chain = GenerateChainWithHeightAndActivatedBip9(5, this.network, new Key(), this.network.Consensus.BIP9Deployments[0]);

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.network);

                var result = powBlockAssembler.ComputeBlockVersion(chain.GetBlock(4));

                Assert.Equal(5, result.Height);
                uint version = ThresholdConditionCache.VersionbitsTopBits;
                int expectedVersion = (int)(version |= (((uint)1) << 19));
                Assert.Equal(expectedVersion, result.Version);
                Assert.NotEqual((int)ThresholdConditionCache.VersionbitsTopBits, result.Version);
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                this.network.Consensus.Options = options;
                this.network.Consensus.BIP9Deployments[0] = null;
                this.network.Consensus.MinerConfirmationWindow = minerConfirmationWindow;
                this.network.Consensus.RuleChangeActivationThreshold = ruleChangeActivationThreshold;
            }
        }

        [Fact]
        public void CreateCoinbase_CreatesCoinbaseTemplateTransaction_AddsToBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new PowConsensusOptions(), () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.network);

                var result = powBlockAssembler.CreateCoinBase(chain.Tip, this.key.ScriptPubKey);

                Assert.NotEmpty(result.Block.Transactions);
                Assert.Equal(-1, result.TxSigOpsCost[0]);
                Assert.Equal(new Money(-1), result.VTxFees[0]);

                var resultingTransaction = result.Block.Transactions[0];
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
            this.ExecuteWithConsensusOptions(new PowConsensusOptions(), () =>
            {
                this.dateTimeProvider.Setup(d => d.GetTimeOffset())
                    .Returns(new DateTimeOffset(new DateTime(2017, 1, 7, 0, 0, 0, DateTimeKind.Utc)));

                var chain = GenerateChainWithHeight(5, this.network, new Key(), new Target(235325239));

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.network);

                var result = powBlockAssembler.UpdateHeaders(chain.Tip);

                Assert.Equal(chain.Tip.HashBlock, result.Header.HashPrevBlock);
                Assert.Equal((uint)1483747200, result.Header.Time);
                Assert.Equal(1.9610088966776103E+35, result.Header.Bits.Difficulty);
                Assert.Equal((uint)0, result.Header.Nonce);
            });
        }

        [Fact]
        public void TestBlockValidity_UsesRuleContextToValidateBlock()
        {
            var newOptions = new PowConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, new Key());
                this.consensusLoop.Setup(c => c.Tip).Returns(chain.GetBlock(5));

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object, new MempoolSchedulerLock(), this.network);

                var blockTemplate = powBlockAssembler.TestBlockValidity();

                Assert.NotNull(this.callbackRuleContext);

                Assert.False(this.callbackRuleContext.CheckMerkleRoot);
                Assert.False(this.callbackRuleContext.CheckPow);
                Assert.Equal(blockTemplate.Block.GetHash(), this.callbackRuleContext.BlockValidationContext.Block.GetHash());
                Assert.Equal(chain.GetBlock(5).HashBlock, this.callbackRuleContext.ConsensusTip.HashBlock);
                Assert.Equal(1500, this.callbackRuleContext.Consensus.Option<PowConsensusOptions>().MaxBlockWeight);
                this.consensusLoop.Verify();
            });
        }

        [Fact]
        public void AddTransactions_WithoutTransactionsInMempool_DoesNotAddEntriesToBlock()
        {
            var newOptions = new PowConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, new Key());
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
                this.txMempool.Setup(t => t.MapTx)
                    .Returns(indexedTransactionSet);

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.network);

                var result = powBlockAssembler.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionNotInblock_AddsTransactionToBlock()
        {
            var newOptions = new PowConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                SetupTxMempool(chain, newOptions, txFee, transaction);

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.network);

                var result = powBlockAssembler.AddTransactions();

                Assert.NotEmpty(result.Block.Transactions);

                Assert.Equal(transaction.ToHex(), result.Block.Transactions[0].ToHex());
                Assert.Equal(1, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionAlreadyInInblock_DoesNotAddTransactionToBlock()
        {
            var newOptions = new PowConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                var entries = SetupTxMempool(chain, newOptions, txFee, transaction);

                var powBlockAssembler = new PowTestBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.txMempool.Object,
                    new MempoolSchedulerLock(), this.network);
                powBlockAssembler.AddInBlockTxEntries(entries);

                var result = powBlockAssembler.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        private void ExecuteWithConsensusOptions(PowConsensusOptions newOptions, Action action)
        {
            var options = this.network.Consensus.Options;
            try
            {
                this.network.Consensus.Options = newOptions;

                action();
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                this.network.Consensus.Options = options;
                this.network.Consensus.BIP9Deployments[0] = null;
            }
        }

        private static ConcurrentChain GenerateChainWithHeightAndActivatedBip9(int blockAmount, Network network, Key key, BIP9DeploymentsParameters parameter, Target bits = null)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
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

        private void SetupConsensusLoop()
        {
            this.callbackRuleContext = null;

            this.consensusLoop.Setup(c => c.ValidateBlock(It.IsAny<RuleContext>()))
                .Callback<RuleContext>(c =>
                {
                    this.callbackRuleContext = c;
                }).Verifiable();

            this.consensusLoop.Setup(c => c.Validator)
                .Returns(this.validator.Object);
        }

        private void SetupValidator()
        {
            this.validator.Setup(v => v.GetProofOfWorkReward(6))
                .Returns(this.powReward);
            this.validator.Setup(v => v.GetBlockWeight(It.IsAny<Block>()))
                .Returns<Block>((block) =>
                {
                    return block.ToBytes().Length;
                });
        }

        private TxMempoolEntry[] SetupTxMempool(ConcurrentChain chain, PowConsensusOptions newOptions, Money txFee, params Transaction[] transactions)
        {
            var txTime = Utils.DateTimeToUnixTime(chain.Tip.Header.BlockTime.AddSeconds(25));
            var lockPoints = new LockPoints()
            {
                Height = 4,
                MaxInputBlock = chain.GetBlock(4),
                Time = chain.GetBlock(4).Header.Time
            };

            var resultingTransactionEntries = new List<TxMempoolEntry>();
            var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
            foreach (var transaction in transactions)
            {
                var txPoolEntry = new TxMempoolEntry(transaction, txFee, txTime, 1, 4, new Money(400000000), false, 2, lockPoints, newOptions);
                indexedTransactionSet.Add(txPoolEntry);
                resultingTransactionEntries.Add(txPoolEntry);
            }


            this.txMempool.Setup(t => t.MapTx)
                .Returns(indexedTransactionSet);

            return resultingTransactionEntries.ToArray();
        }

        private class PowTestBlockAssembler : PowBlockAssembler
        {
            public PowTestBlockAssembler(
                IConsensusLoop consensusLoop,
                IDateTimeProvider dateTimeProvider,
                ILoggerFactory loggerFactory,
                ITxMempool mempool,
                MempoolSchedulerLock mempoolLock,
                Network network,
                AssemblerOptions options = null)
                : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network, options)
            {
                base.block = this.blockTemplate.Block;
            }

            public void AddInBlockTxEntries(params TxMempoolEntry[] entries)
            {
                foreach (var entry in entries)
                {
                    base.inBlock.Add(entry);
                }
            }

            public (int Height, int Version) ComputeBlockVersion(ChainedBlock chainTip)
            {
                base.ChainTip = chainTip;

                base.ComputeBlockVersion();

                return (base.height, base.block.Header.Version);
            }

            public BlockTemplate CreateCoinBase(ChainedBlock chainTip, Script scriptPubKeyIn)
            {
                base.scriptPubKey = scriptPubKeyIn;
                base.ChainTip = chainTip;

                base.CreateCoinbase();

                base.blockTemplate.Block = base.block;

                return base.blockTemplate;
            }

            public Block UpdateHeaders(ChainedBlock chainTip)
            {
                base.ChainTip = chainTip;

                base.UpdateHeaders();

                return this.block;
            }

            public new BlockTemplate TestBlockValidity()
            {
                base.TestBlockValidity();

                return base.blockTemplate;
            }

            public (Block Block, int Selected, int Updated) AddTransactions()
            {
                int selected;
                int updated;
                base.AddTransactions(out selected, out updated);

                return (base.block, selected, updated);
            }
        }
    }
}
