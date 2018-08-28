using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CoinbaseHeightRuleTest : TestConsensusRulesUnitTestBase
    {
        public CoinbaseHeightRuleTest()
        {
        }

        [Fact]
        public async Task RunAsync_BestBlockAvailable_BadCoinBaseHeight_ThrowsBadCoinbaseHeightConsensusErrorExceptionAsync()
        {
            Block blockToValidate = this.network.CreateBlock();

            this.ruleContext.ValidationContext.BlockToValidate = blockToValidate;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(blockToValidate.Header, blockToValidate.Header.GetHash(), 0);

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(3))));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CoinbaseHeightRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseHeight, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BestBlockUnAvailable_BadCoinBaseHeight_ThrowsBadCoinbaseHeightConsensusErrorExceptionAsync()
        {
            Block blockToValidate = this.network.CreateBlock();
            
            this.ruleContext.ValidationContext.BlockToValidate = blockToValidate;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(blockToValidate.Header, blockToValidate.Header.GetHash(), 0);

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(3))));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CoinbaseHeightRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadCoinbaseHeight, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_CorrectCoinBaseHeight_DoesNotThrowExceptionAsync()
        {
            Block blockToValidate = this.network.CreateBlock();

            this.ruleContext.ValidationContext.BlockToValidate = blockToValidate;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(blockToValidate.Header, blockToValidate.Header.GetHash(), 4);

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn(new Script(Op.GetPushOp(4))));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(transaction);

            await this.consensusRules.RegisterRule<CoinbaseHeightRule>().RunAsync(this.ruleContext);
        }
    }
}