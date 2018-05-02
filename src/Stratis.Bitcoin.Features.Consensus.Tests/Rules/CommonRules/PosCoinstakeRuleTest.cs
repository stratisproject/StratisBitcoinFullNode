using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosCoinstakeRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public PosCoinstakeRuleTest()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinBaseNotEmpty_NoOutputsOnTransaction_ThrowsBadStakeBlockConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadStakeBlock, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinBaseNotEmpty_TransactionNotEmpty_ThrowsBadStakeBlockConsensusErrorExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(new Money(1), (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadStakeBlock, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_MultipleCoinStakeAfterSecondTransaction_ThrowsBadMultipleCoinstakeConsensusErrorExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadMultipleCoinstake.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_TransactionTimestampAfterBlockTimeStamp_ThrowsBlockTimeBeforeTrxConsensusErrorExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = (uint)1483747201;

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimeBeforeTrx, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_TransactionTimestampAfterBlockTimeStamp_ThrowsBlockTimeBeforeTrxConsensusErrorExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
            this.ruleContext.BlockValidationContext.Block.Transactions[0].Time = (uint)1483747201;

            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimeBeforeTrx, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ValidBlock_DoesNotThrowExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
            this.ruleContext.BlockValidationContext.Block.Transactions[0].Time = (uint)1483747200;
            this.ruleContext.BlockValidationContext.Block.Transactions[1].Time = (uint)1483747200;

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            await this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_ValidBlock_DoesNotThrowExceptionAsync()
        {
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);
            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)1483747200;
            this.ruleContext.BlockValidationContext.Block.Transactions[0].Time = (uint)1483747200;

            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.BlockValidationContext.Block));

            await this.consensusRules.RegisterRule<PosCoinstakeRule>().RunAsync(this.ruleContext);
        }
    }
}
