using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class TransactionLocktimeActivationRuleTest : TestConsensusRulesUnitTestBase
    {
        public TransactionLocktimeActivationRuleTest()
        {
            this.ChainIndexer = GenerateChainWithHeight(5, this.network);
        }

        [Fact]
        public void RunAsync_DoesNotHaveBIP113Flag_TransactionNotFinal_ThrowsBadTransactionNonFinalConsensusErrorExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags();

            Block block = this.network.CreateBlock();
            Transaction transaction = this.network.CreateTransaction();
            transaction.LockTime = new DateTimeOffset(new DateTime(2018, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            transaction.Inputs.Add(new TxIn() { Sequence = 15 });
            block.AddTransaction(transaction);
            block.AddTransaction(CreateCoinStakeTransaction(this.network, new Key(), 6, this.ChainIndexer.GetHeader(5).HashBlock));
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = block,
                ChainedHeaderToValidate = this.ChainIndexer.GetHeader(4)
            };

            this.ruleContext.ValidationContext.BlockToValidate.Header.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionNonFinal, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_HasBIP113Flag_TransactionNotFinal_ThrowsBadTransactionNonFinalConsensusErrorExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast };
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Block block = this.network.CreateBlock();
            Transaction transaction = this.network.CreateTransaction();
            transaction.LockTime = new DateTimeOffset(new DateTime(2018, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            transaction.Inputs.Add(new TxIn() { Sequence = 15 });
            block.AddTransaction(transaction);
            block.AddTransaction(CreateCoinStakeTransaction(this.network, new Key(), 6, this.ChainIndexer.GetHeader(5).HashBlock));
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = block,
                ChainedHeaderToValidate = this.ChainIndexer.GetHeader(4)
            };

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadTransactionNonFinal, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_DoesNotHaveBIP113Flag_TransactionFinal_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags();

            Block block = this.network.CreateBlock();
            Transaction transaction = this.network.CreateTransaction();
            block.AddTransaction(transaction);
            block.AddTransaction(CreateCoinStakeTransaction(this.network, new Key(), 6, this.ChainIndexer.GetHeader(5).HashBlock));
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = block,
                ChainedHeaderToValidate = this.ChainIndexer.GetHeader(4)
            };

            this.ruleContext.ValidationContext.BlockToValidate.Header.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            
            this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().Run(this.ruleContext);
        }

        [Fact]
        public void RunAsync_HasBIP113Flag_TransactionFinal_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Flags = new Base.Deployments.DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast };
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Block block = this.network.CreateBlock();
            Transaction transaction = this.network.CreateTransaction();
            block.AddTransaction(transaction);
            block.AddTransaction(CreateCoinStakeTransaction(this.network, new Key(), 6, this.ChainIndexer.GetHeader(5).HashBlock));
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = block,
                ChainedHeaderToValidate = this.ChainIndexer.GetHeader(4)
            };

            this.consensusRules.RegisterRule<TransactionLocktimeActivationRule>().Run(this.ruleContext);
        }

        private static Transaction CreateCoinStakeTransaction(Network network, Key key, int height, uint256 prevout)
        {
            var coinStake = network.CreateTransaction();
            coinStake.Time = (uint)18276127;
            coinStake.AddInput(new TxIn(new OutPoint(prevout, 1)));
            coinStake.AddOutput(new TxOut(0, new Script()));
            coinStake.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
            return coinStake;
        }
    }
}
