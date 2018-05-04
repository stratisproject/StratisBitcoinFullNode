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
    public class PosBlockAssemblerTest : LogsTestBase
    {
        private RuleContext callbackRuleContext = null;
        private Mock<IConsensusLoop> consensusLoop;
        private Mock<IDateTimeProvider> dateTimeProvider;
        private Key key;
        private Mock<ITxMempool> mempool;
        private Network network;
        private Money powReward;
        private Mock<IStakeChain> stakeChain;
        private Mock<IStakeValidator> stakeValidator;
        private Mock<IPosConsensusValidator> validator;

        public PosBlockAssemblerTest()
        {
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.mempool = new Mock<ITxMempool>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.stakeChain = new Mock<IStakeChain>();
            this.powReward = new Money(100 * 1000 * 1000);
            this.validator = new Mock<IPosConsensusValidator>();
            this.network = Network.StratisTest;
            this.key = new Key();

            SetupValidator();
            SetupConsensusLoop();
        }

        [Fact]
        public void TestBlockValidity_DoesNotValidateBlockUsingRuleContext()
        {
            var newOptions = new PosConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, new Key());
                this.consensusLoop.Setup(c => c.Tip)
               .Returns(chain.GetBlock(5));

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                posBlockAssembler.TestBlockValidity();

                this.consensusLoop.Verify(c => c.ValidateBlock(It.IsAny<RuleContext>()), Times.Exactly(0));
            });
        }

        [Fact]
        public void UpdateHeaders_UsingChainAndNetwork_PreparesStakeBlockHeaders()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                this.dateTimeProvider.Setup(d => d.GetTimeOffset())
                    .Returns(new DateTimeOffset(new DateTime(2017, 1, 7, 0, 0, 0, DateTimeKind.Utc)));

                var chain = GenerateChainWithHeight(5, this.network, new Key());
                var assemblerOptions = new AssemblerOptions()
                {
                    IsProofOfStake = true
                };

                this.stakeValidator.Setup(s => s.GetNextTargetRequired(this.stakeChain.Object, chain.Tip, this.network.Consensus, true))
                    .Returns(new Target(new uint256(1123123123)))
                    .Verifiable();

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                var result = posBlockAssembler.UpdateHeaders(chain.Tip);

                Assert.Equal(chain.Tip.HashBlock, result.Header.HashPrevBlock);
                Assert.Equal((uint)1483747200, result.Header.Time);
                Assert.Equal(2.400408204198463E+58, result.Header.Bits.Difficulty);
                Assert.Equal((uint)0, result.Header.Nonce);
                this.stakeValidator.Verify();
            });
        }

        [Fact]
        public void CreateNewBlock_WithScript_ReturnsBlockTemplate()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp()).Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());
                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);

                SetupTxMempool(chain, this.network.Consensus.Options as PosConsensusOptions, txFee, transaction);

                this.stakeValidator.Setup(s => s.GetNextTargetRequired(this.stakeChain.Object, chain.Tip, this.network.Consensus, true))
                    .Returns(new Target(new uint256(1123123123)))
                    .Verifiable();

                var posBlockAssembler = new PosBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.mempool.Object, new MempoolSchedulerLock(), this.network, this.stakeChain.Object, this.stakeValidator.Object);
                var blockTemplate = posBlockAssembler.Build(chain.Tip, this.key.ScriptPubKey);

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
            var newOptions = new PosConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp()).Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());
                this.consensusLoop.Setup(c => c.Tip).Returns(chain.GetBlock(5));

                this.stakeValidator.Setup(s => s.GetNextTargetRequired(this.stakeChain.Object, chain.Tip, this.network.Consensus, true))
                    .Returns(new Target(new uint256(1123123123)))
                    .Verifiable();

                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);

                SetupTxMempool(chain, newOptions, txFee, transaction);

                var posBlockAssembler = new PosBlockAssembler(this.consensusLoop.Object, this.dateTimeProvider.Object, this.LoggerFactory.Object, this.mempool.Object, new MempoolSchedulerLock(), this.network, this.stakeChain.Object, this.stakeValidator.Object);

                var blockTemplate = posBlockAssembler.Build(chain.Tip, this.key.ScriptPubKey);

                this.consensusLoop.Verify(c => c.ValidateBlock(It.IsAny<RuleContext>()), Times.Exactly(0));
                Assert.Null(this.callbackRuleContext);
                this.stakeValidator.Verify();
            });
        }

        [Fact]
        public void ComputeBlockVersion_UsingChainTipAndConsensus_NoBip9DeploymentActive_UpdatesHeightAndVersion()
        {
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, new Key());

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object,
                                                 this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                var result = posBlockAssembler.ComputeBlockVersion(chain.GetBlock(4));

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
                var newOptions = new PosConsensusOptions();
                this.network.Consensus.Options = newOptions;
                this.network.Consensus.BIP9Deployments[0] = new BIP9DeploymentsParameters(19,
                    new DateTimeOffset(new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
                this.network.Consensus.MinerConfirmationWindow = 2;
                this.network.Consensus.RuleChangeActivationThreshold = 2;

                var chain = GenerateChainWithHeightAndActivatedBip9(5, this.network, new Key(), this.network.Consensus.BIP9Deployments[0]);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                var result = posBlockAssembler.ComputeBlockVersion(chain.GetBlock(4));

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
            this.ExecuteWithConsensusOptions(new PosConsensusOptions(), () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(new DateTime(2017, 1, 7, 0, 0, 1, DateTimeKind.Utc).ToUnixTimestamp());

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                var result = posBlockAssembler.CreateCoinBase(chain.Tip, this.key.ScriptPubKey);

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
        public void AddTransactions_WithoutTransactionsInMempool_DoesNotAddEntriesToBlock()
        {
            var newOptions = new PosConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, new Key());
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
                this.mempool.Setup(t => t.MapTx)
                    .Returns(indexedTransactionSet);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object,
                                                 this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                var result = posBlockAssembler.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionNotInblock_AddsTransactionToBlock()
        {
            var newOptions = new PosConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                SetupTxMempool(chain, newOptions, txFee, transaction);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);

                var result = posBlockAssembler.AddTransactions();

                Assert.NotEmpty(result.Block.Transactions);

                Assert.Equal(transaction.ToHex(), result.Block.Transactions[0].ToHex());
                Assert.Equal(1, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        [Fact]
        public void AddTransactions_TransactionAlreadyInInblock_DoesNotAddTransactionToBlock()
        {
            var newOptions = new PosConsensusOptions() { MaxBlockWeight = 1500 };

            this.ExecuteWithConsensusOptions(newOptions, () =>
            {
                var chain = GenerateChainWithHeight(5, this.network, this.key);
                this.consensusLoop.Setup(c => c.Tip)
                    .Returns(chain.GetBlock(5));
                var transaction = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
                var txFee = new Money(1000);
                var entries = SetupTxMempool(chain, newOptions, txFee, transaction);

                var posBlockAssembler = new PosTestBlockAssembler(this.consensusLoop.Object, this.network, new MempoolSchedulerLock(), this.mempool.Object, this.dateTimeProvider.Object, this.stakeChain.Object, this.stakeValidator.Object, this.LoggerFactory.Object);
                posBlockAssembler.AddInBlockTxEntries(entries);

                var result = posBlockAssembler.AddTransactions();

                Assert.Empty(result.Block.Transactions);
                Assert.Equal(0, result.Selected);
                Assert.Equal(0, result.Updated);
            });
        }

        private void ExecuteWithConsensusOptions(PosConsensusOptions newOptions, Action action)
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

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network, Key key)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
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
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
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

        private TxMempoolEntry[] SetupTxMempool(ConcurrentChain chain, PosConsensusOptions newOptions, Money txFee, params Transaction[] transactions)
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


            this.mempool.Setup(t => t.MapTx)
                .Returns(indexedTransactionSet);

            return resultingTransactionEntries.ToArray();
        }

        private class PosTestBlockAssembler : PosBlockAssembler
        {
            public PosTestBlockAssembler(
                IConsensusLoop consensusLoop,
                Network network,
                MempoolSchedulerLock mempoolLock,
                ITxMempool mempool,
                IDateTimeProvider dateTimeProvider,
                IStakeChain stakeChain,
                IStakeValidator stakeValidator,
                ILoggerFactory loggerFactory)
                : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network, stakeChain, stakeValidator)
            {
                base.block = this.blockTemplate.Block;
            }

            public new void TestBlockValidity()
            {
                base.TestBlockValidity();
            }

            public Block UpdateHeaders(ChainedBlock chainTip)
            {
                base.ChainTip = chainTip;

                base.UpdateHeaders();

                return this.block;
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

            public (Block Block, int Selected, int Updated) AddTransactions()
            {
                int selected;
                int updated;
                base.AddTransactions(out selected, out updated);

                return (base.block, selected, updated);
            }

            public void AddInBlockTxEntries(params TxMempoolEntry[] entries)
            {
                foreach (var entry in entries)
                {
                    base.inBlock.Add(entry);
                }
            }
        }
    }
}