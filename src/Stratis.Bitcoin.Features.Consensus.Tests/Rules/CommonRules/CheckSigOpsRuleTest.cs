using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CheckSigOpsRuleTest : TestConsensusRulesUnitTestBase
    {
        private ConsensusOptions options;

        public CheckSigOpsRuleTest()
        {
            this.options = this.network.Consensus.Options;
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
        }

        [Fact]
        public async Task RunAsync_SingleTransactionInputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 7;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op, op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSigOps, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionInputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 7;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSigOps, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionOutputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 7;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op, op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSigOps, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionOutputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 7;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSigOps, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_CombinedTransactionInputOutputSigOpsCountAboveThresHold_ThrowsBadBlockSigOpsConsensusErrorExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 7;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSigOps, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionInputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op, op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionInputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionOutputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op, op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionOutputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_CombinedTransactionInputOutputSigOpsCountAtThresHold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 8;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionInputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op, op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionInputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_SingleTransactionOutputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op, op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_MultipleTransactionOutputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_CombinedTransactionInputOutputSigOpsCountBelowThreshold_DoesNotThrowExceptionAsync()
        {
            this.options.MaxBlockSigopsCost = 9;
            this.options.WitnessScaleFactor = 2;

            var transaction = new Transaction();
            var op = new Op() { Code = OpcodeType.OP_CHECKSIG };
            transaction.Inputs.Add(new TxIn(new Script(op, op)));
            transaction.Outputs.Add(new TxOut(new Money(1), new Script(op, op)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CheckSigOpsRule>().RunAsync(this.ruleContext);
        }
    }
}
