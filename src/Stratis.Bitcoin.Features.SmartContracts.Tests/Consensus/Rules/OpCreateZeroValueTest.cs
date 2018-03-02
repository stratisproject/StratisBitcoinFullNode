using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.SmartContracts;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class OpCreateZeroValueTest
    {
        public OpCreateZeroValueTest()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        [Fact]
        public async Task OpCreateZeroValueRule_SuccessAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            OpCreateZeroValueRule rule = testContext.CreateRule<OpCreateZeroValueRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block();

            var gasPriceSatoshis = 20;
            var gasLimit = 4000000;
            var smartContractCreateFee = 0;

            var carrier = SmartContractCarrier.CreateContract(1, new byte[] { }, (ulong)gasPriceSatoshis, (Gas)gasLimit);
            var serialized = carrier.Serialize();
            var script = new Script(serialized);

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                new Transaction
                {
                    Outputs =
                    {
                        new TxOut(smartContractCreateFee, script)
                    }
                }
            };

            await rule.RunAsync(context);
        }

        [Fact]
        public async Task OpCreateZeroValueRule_FailureAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            OpCreateZeroValueRule rule = testContext.CreateRule<OpCreateZeroValueRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = new Block();

            var gasPriceSatoshis = 20;
            var gasLimit = 4000000;
            var smartContractCreateFee = 1;

            var carrier = SmartContractCarrier.CreateContract(1, new byte[] { }, (ulong)gasPriceSatoshis, (Gas)gasLimit);
            var serialized = carrier.Serialize();
            var script = new Script(serialized);

            context.BlockValidationContext.Block.Transactions = new List<Transaction>
            {
                new Transaction
                {
                    Outputs =
                    {
                        new TxOut(smartContractCreateFee, script)
                    }
                }
            };

            var error = Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
        }
    }
}
