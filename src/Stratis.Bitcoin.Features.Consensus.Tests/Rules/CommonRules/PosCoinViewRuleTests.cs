﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    /// <summary>
    /// Test cases related to the <see cref="PosCoinviewRule"/>.
    /// </summary>
    public class PosCoinViewRuleTests : TestPosConsensusRulesUnitTestBase
    {
        /// <summary>
        /// Creates the consensus manager used by <see cref="PosCoinViewRuleFailsAsync"/>.
        /// </summary>
        /// <param name="unspentOutputs">The dictionary used to mock up the <see cref="ICoinView"/>.</param>
        /// <returns>The constructed consensus manager.</returns>
        private async Task<ConsensusManager> CreateConsensusManagerAsync(Dictionary<uint256, UnspentOutputs> unspentOutputs)
        {
            var nodeSettings = Configuration.NodeSettings.Default(this.network);
            this.consensusSettings = new ConsensusSettings(nodeSettings);
            var chainState = new ChainState(new InvalidBlockHashStore(this.dateTimeProvider.Object));
            var initialBlockDownloadState = new InitialBlockDownloadState(chainState, this.network, this.consensusSettings, this.checkpoints.Object);

            // Register POS consensus rules.
            this.network.Consensus.Rules = new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().GetRules();
            this.consensusRules = new TestPosConsensusRules(this.network, this.loggerFactory.Object, this.dateTimeProvider.Object,
                this.concurrentChain, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object, this.coinView.Object,
                new Mock<ILookaheadBlockPuller>().Object, this.stakeChain.Object, this.stakeValidator.Object);
            this.consensusRules.Register();

            var headerValidator = new HeaderValidator(this.consensusRules, this.loggerFactory.Object);
            var integrityValidator = new IntegrityValidator(this.consensusRules, this.loggerFactory.Object);
            var partialValidator = new PartialValidation(this.consensusRules, this.loggerFactory.Object);

            // Create consensus manager.
            var consensus = new ConsensusManager(this.network, this.loggerFactory.Object, chainState, headerValidator, integrityValidator,
                partialValidator, this.checkpoints.Object, this.consensusSettings, this.consensusRules, new Mock<IFinalizedBlockHeight>().Object, new Signals.Signals(),
                new Mock<IPeerBanning>().Object, nodeSettings, this.dateTimeProvider.Object, initialBlockDownloadState, this.concurrentChain, new Mock<IBlockStore>().Object);

            // Mock the coinviews "FetchCoinsAsync" method. We will use the "unspentOutputs" dictionary to track spendable outputs.
            this.coinView.Setup(d => d.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .Returns((uint256[] txIds, CancellationToken cancel) => Task.Run(() => {

                    var result = new UnspentOutputs[txIds.Length];

                    for (int i = 0; i < txIds.Length; i++)
                        result[i] = unspentOutputs.TryGetValue(txIds[i], out UnspentOutputs unspent) ? unspent : null;

                    return new FetchCoinsResponse(result, this.concurrentChain.Tip.HashBlock);
                }));

            // Mock the coinviews "GetTipHashAsync" method.
            this.coinView.Setup(d => d.GetTipHashAsync(It.IsAny<CancellationToken>())).Returns(() => Task.Run(() => {
                return this.concurrentChain.Tip.HashBlock;
            }));

            // Since we are mocking the stake validator ensure that GetNextTargetRequired returns something sensible. Otherwise we get the "bad-diffbits" error.
            this.stakeValidator.Setup(s => s.GetNextTargetRequired(It.IsAny<IStakeChain>(), It.IsAny<ChainedHeader>(), It.IsAny<IConsensus>(), It.IsAny<bool>()))
                .Returns(this.network.Consensus.PowLimit);

            // Since we are mocking the stakechain ensure that the Get returns a BlockStake. Otherwise this results in "previous stake is not found".
            this.stakeChain.Setup(d => d.Get(It.IsAny<uint256>())).Returns(new BlockStake()
            {
                Flags = BlockFlag.BLOCK_PROOF_OF_STAKE,
                StakeModifierV2 = 0,
                StakeTime = (this.concurrentChain.Tip.Header.Time + 60) & ~PosTimeMaskRule.StakeTimestampMask
            });

            // Since we are mocking the checkpoints we ensure that this method returns true.
            this.checkpoints.Setup(d => d.CheckHardened(It.IsAny<int>(), It.IsAny<uint256>())).Returns(true);

            // Initialize the consensus manager.
            await consensus.InitializeAsync(this.concurrentChain.Tip);

            return consensus;
        }

        /// <summary>
        /// Tests whether an error is raised when a miner attempts to stake an output which he can't spend with his private key.
        /// </summary>
        /// <remarks>
        /// <para>Create a "previous transaction" with 2 outputs. The first output is sent to miner 2 and the second output is sent to miner 1.
        /// Now miner 2 creates a proof of stake block with coinstake transaction which will have two inputs corresponding to both the
        /// outputs of the previous transaction. The coinstake transaction will be just two outputs, first is the coinstake marker and
        /// the second is normal pay to public key that belongs to miner 2 with value that equals to the sum of the inputs.
        /// The testable outcome is whether the consensus engine accepts such a block. Obviously, the test should fail if the block is accepted.
        /// </para><para>
        /// We use <see cref="ConsensusManager.BlockMinedAsync(Block)"/> to run partial validation and full validation rules and expect that
        /// the rules engine will reject the block with the specific error of <see cref="ConsensusErrors.BadTransactionScriptError"/>,
        /// which confirms that there was no valid signature on the second input - which corresponds to the output sent to miner 1.
        /// </para></remarks>
        [Fact]
        public async Task PosCoinViewRuleFailsAsync()
        {
            var unspentOutputs = new Dictionary<uint256, UnspentOutputs>();

            ConsensusManager consensusManager = await this.CreateConsensusManagerAsync(unspentOutputs);

            // The keys used by miner 1 and miner 2.
            var minerKey1 = new Key();
            var minerKey2 = new Key();

            // The scriptPubKeys (P2PK) of the miners.
            Script scriptPubKey1 = minerKey1.PubKey.ScriptPubKey;
            Script scriptPubKey2 = minerKey2.PubKey.ScriptPubKey;

            // Create the block that we want to validate.
            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();

            // Add dummy first transaction.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(TxIn.CreateCoinbase(this.concurrentChain.Tip.Height + 1));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            // Add first transaction to block.
            block.Transactions.Add(transaction);

            // Create a previous transaction with scriptPubKey outputs.
            Transaction prevTransaction = this.network.CreateTransaction();
            // Coins sent to miner 2.
            prevTransaction.Outputs.Add(new TxOut(Money.COIN * 5_000_000, scriptPubKey2));
            // Coins sent to miner 1.
            prevTransaction.Outputs.Add(new TxOut(Money.COIN * 10_000_000, scriptPubKey1));

            // Record the spendable outputs.
            unspentOutputs[prevTransaction.GetHash()] = new UnspentOutputs(1, prevTransaction);

            // Create coin stake transaction.
            Transaction coinstakeTransaction = this.network.CreateTransaction();

            coinstakeTransaction.Inputs.Add(new TxIn(new OutPoint(prevTransaction, 0)));
            coinstakeTransaction.Inputs.Add(new TxIn(new OutPoint(prevTransaction, 1)));

            // Coinstake marker.
            coinstakeTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            // Normal pay to public key that belongs to the second miner with value that
            // equals to the sum of the inputs.
            coinstakeTransaction.Outputs.Add(new TxOut(Money.COIN * 15_000_000, scriptPubKey2));

            // The second miner signs the first transaction input which requires minerKey2.
            // Miner 2 will not have minerKey1 so we leave the second ScriptSig empty/invalid.
            new TransactionBuilder(this.network)
                .AddKeys(minerKey2)
                .AddCoins(new Coin(new OutPoint(prevTransaction, 0), prevTransaction.Outputs[0]))
                .SignTransactionInPlace(coinstakeTransaction);

            Assert.True(coinstakeTransaction.IsCoinStake);

            // Add second transaction to block.
            block.Transactions.Add(coinstakeTransaction);

            // Finalize the block and add it to the chain.
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Time = (this.concurrentChain.Tip.Header.Time + 60) & ~PosTimeMaskRule.StakeTimestampMask;
            block.Header.Bits = block.Header.GetWorkRequired(this.network, this.concurrentChain.Tip);
            block.SetPrivatePropertyValue("BlockSize", 1L);
            block.Transactions[0].Time = block.Header.Time;
            block.Transactions[1].Time = block.Header.Time;
            block.UpdateMerkleRoot();
            Assert.True(BlockStake.IsProofOfStake(block));
            // Add a signature to the block.
            ECDSASignature signature = minerKey2.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            // Execute the rule and check the outcome against what is expected.
            this.ruleContext.ValidationContext.Block = block;
            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(block.Header, block.Header.GetHash(), this.concurrentChain.Tip);
            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => {
                await this.consensusRules.ValidateAndExecuteAsync(this.ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadTransactionScriptError.Message, error.Message);
        }
    }
}
