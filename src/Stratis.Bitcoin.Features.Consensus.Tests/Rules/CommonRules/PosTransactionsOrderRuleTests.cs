using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosTransactionsOrderRuleTests
    {
        private readonly PosTransactionsOrderRule rule;

        public PosTransactionsOrderRuleTests()
        {
            this.rule = new PosTransactionsOrderRule();
            this.rule.Logger = new Mock<ILogger>().Object;
        }

        [Fact]
        public void PassesIfAllCorrect()
        {
            var validationContext = new ValidationContext();

            validationContext.BlockToValidate = new Block()
            {
                Transactions = new List<Transaction>()
                {
                    this.GetRandomTx(),
                    this.GetRandomTx()
                }
            };

            var header = new ProvenBlockHeader();

            header.SetPrivateVariableValue("coinstake", validationContext.BlockToValidate.Transactions[1]);

            validationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), 100);

            this.rule.Run(new RuleContext(validationContext, DateTimeOffset.Now));
        }

        [Fact]
        public void FailsIfTxCountIsLow()
        {
            var validationContext = new ValidationContext();

            validationContext.BlockToValidate = new Block() { Transactions = new List<Transaction>() { this.GetRandomTx() } };

            ConsensusErrorException expectedEx = null;

            try
            {
                this.rule.Run(new RuleContext(validationContext, DateTimeOffset.Now));
            }
            catch (ConsensusErrorException e)
            {
                expectedEx = e;
            }

            Assert.NotNull(expectedEx);
            Assert.Equal(ConsensusErrors.InvalidTxCount.Code, expectedEx.ConsensusError.Code);
        }

        [Fact]
        public void FailesIfCoinstakeTxMissmatch()
        {
            var validationContext = new ValidationContext();

            validationContext.BlockToValidate = new Block()
            {
                Transactions = new List<Transaction>()
                {
                    this.GetRandomTx(),
                    this.GetRandomTx()
                }
            };

            var header = new ProvenBlockHeader();

            header.SetPrivateVariableValue("coinstake", this.GetRandomTx());

            validationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), 100);

            ConsensusErrorException expectedEx = null;

            try
            {
                this.rule.Run(new RuleContext(validationContext, DateTimeOffset.Now));
            }
            catch (ConsensusErrorException e)
            {
                expectedEx = e;
            }

            Assert.NotNull(expectedEx);
            Assert.Equal(ConsensusErrors.PHCoinstakeMissmatch.Code, expectedEx.ConsensusError.Code);
        }

        private Transaction GetRandomTx()
        {
            var tx = new Transaction();
            tx.AddOutput(1000, new Script(RandomUtils.GetBytes(50)));

            return tx;
        }
    }
}
