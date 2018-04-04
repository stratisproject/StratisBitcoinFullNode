using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateStakeRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public CalculateStakeRuleTest() : base()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_SetsStake_SetsNextWorkRequiredAsync()
        {
            var ruleContext = new RuleContext()
            {
                Stake = null,
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                    {
                        Transactions = new List<NBitcoin.Transaction>()
                        {
                            new NBitcoin.Transaction(),
                            CreateCoinStakeTransaction(this.network, new Key(), 6, this.concurrentChain.GetBlock(5).HashBlock)
                        }
                    },
                    ChainedBlock = this.concurrentChain.GetBlock(4)
                }
            };
            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(3),
                ruleContext.Consensus,
                true))
                .Returns(new Target(0x1f111115))
                .Verifiable();

            var rule = this.consensusRules.RegisterRule<CalculateStakeRule>();

            await rule.RunAsync(ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull(ruleContext.Stake);
            Assert.Equal(BlockFlag.BLOCK_PROOF_OF_STAKE, ruleContext.Stake.BlockStake.Flags);
            Assert.Equal(uint256.Zero, ruleContext.Stake.BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, ruleContext.Stake.BlockStake.HashProof);
            Assert.Equal((uint)18276127, ruleContext.Stake.BlockStake.StakeTime);
            Assert.Equal(this.concurrentChain.GetBlock(5).HashBlock, ruleContext.Stake.BlockStake.PrevoutStake.Hash);
            Assert.Equal(new Target(0x1f111115).Difficulty, ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_DoNotCheckPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            var ruleContext = new RuleContext()
            {
                Stake = null,
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                    {
                        Transactions = new List<NBitcoin.Transaction>()
                        {
                            new NBitcoin.Transaction()
                        }
                    },
                    ChainedBlock = this.concurrentChain.GetBlock(4)
                },
                CheckPow = false
            };
            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(3),
                ruleContext.Consensus,
                false))
                .Returns(new Target(0x1f111115))
                .Verifiable();

            var rule = this.consensusRules.RegisterRule<CalculateStakeRule>();

            await rule.RunAsync(ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull(ruleContext.Stake);
            Assert.Equal(0, (int)ruleContext.Stake.BlockStake.Flags);
            Assert.Equal(uint256.Zero, ruleContext.Stake.BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, ruleContext.Stake.BlockStake.HashProof);
            Assert.Equal((uint)0, ruleContext.Stake.BlockStake.StakeTime);
            Assert.Null(ruleContext.Stake.BlockStake.PrevoutStake);
            Assert.Equal(new Target(0x1f111115).Difficulty, ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_ValidPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            var ruleContext = new RuleContext()
            {
                Stake = null,
                BlockValidationContext = new BlockValidationContext()
                {
                    ChainedBlock = this.concurrentChain.Tip
                },
                CheckPow = true,
                Consensus = this.network.Consensus
            };
            ruleContext.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);

            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(1),
                ruleContext.Consensus,
                false))
                .Returns(new Target(0x1f111115))
                .Verifiable();

            var rule = this.consensusRules.RegisterRule<CalculateStakeRule>();

            await rule.RunAsync(ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull(ruleContext.Stake);
            Assert.Equal(0, (int)ruleContext.Stake.BlockStake.Flags);
            Assert.Equal(uint256.Zero, ruleContext.Stake.BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, ruleContext.Stake.BlockStake.HashProof);
            Assert.Equal((uint)0, ruleContext.Stake.BlockStake.StakeTime);
            Assert.Null(ruleContext.Stake.BlockStake.PrevoutStake);
            Assert.Equal(new Target(0x1f111115).Difficulty, ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Stake = null,
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                        {
                            Transactions = new List<NBitcoin.Transaction>()
                        {
                            new NBitcoin.Transaction()
                        }
                        },
                        ChainedBlock = this.concurrentChain.GetBlock(4)
                    },
                    CheckPow = true
                };

                var rule = this.consensusRules.RegisterRule<CalculateStakeRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.HighHash.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.HighHash.Message, exception.ConsensusError.Message);
        }

        private static Transaction CreateCoinStakeTransaction(Network network, Key key, int height, uint256 prevout)
        {
            var coinStake = new Transaction();
            coinStake.Time = (uint)18276127;
            coinStake.AddInput(new TxIn(new OutPoint(prevout, 1)));
            coinStake.AddOutput(new TxOut(0, new Script()));
            coinStake.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
            return coinStake;
        }
    }
}
