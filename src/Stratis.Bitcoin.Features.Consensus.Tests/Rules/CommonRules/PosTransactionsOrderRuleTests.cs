using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosTransactionsOrderRuleTests : TestPosConsensusRulesUnitTestBase
    {
        private readonly PosTransactionsOrderRule rule;

        private readonly ConsensusRuleEngine engine;

        public PosTransactionsOrderRuleTests()
        {
            this.rule = new PosTransactionsOrderRule();
            this.rule.Logger = new Mock<ILogger>().Object;

            this.engine = new PosConsensusRuleEngine(this.network, this.loggerFactory.Object, DateTimeProvider.Default,
                    this.ChainIndexer, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object, this.coinView.Object, this.stakeChain.Object,
                    this.stakeValidator.Object, this.chainState.Object, new InvalidBlockHashStore(this.dateTimeProvider.Object), new Mock<INodeStats>().Object,
                    this.rewindDataIndexStore.Object, this.asyncProvider).Register();

            this.rule.Parent = this.engine;
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

            validationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.engine.Checkpoints.GetLastCheckpointHeight() + 1);

            this.rule.Run(new RuleContext(validationContext, DateTimeOffset.Now));
        }

        [Fact]
        public void FailsIfTxCountIsLow()
        {
            var validationContext = new ValidationContext();

            validationContext.BlockToValidate = new Block() { Transactions = new List<Transaction>() { this.GetRandomTx() } };

            var header = new ProvenBlockHeader();

            header.SetPrivateVariableValue("coinstake", this.GetRandomCoinstakeTx());

            validationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.engine.Checkpoints.GetLastCheckpointHeight() + this.network.Consensus.LastPOWBlock + 1);

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
                    this.GetRandomCoinstakeTx()
                }
            };

            var header = new ProvenBlockHeader();

            header.SetPrivateVariableValue("coinstake", this.GetRandomCoinstakeTx());

            validationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.engine.Checkpoints.GetLastCheckpointHeight() + this.network.Consensus.LastPOWBlock + 1);

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

        [Fact]
        public void FailesIfPowHeaderIsGivenAfterLastCheckpoint()
        {
            var validationContext = new ValidationContext();

            var header = new BlockHeader();

            validationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.engine.Checkpoints.GetLastCheckpointHeight() + this.network.Consensus.LastPOWBlock + 1);

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
            Assert.Equal(ConsensusErrors.ProofOfWorkTooHigh.Code, expectedEx.ConsensusError.Code);
        }

        private Transaction GetRandomTx()
        {
            var tx = new Transaction();
            tx.AddOutput(1000, new Script(RandomUtils.GetBytes(50)));

            return tx;
        }

        private Transaction GetRandomCoinstakeTx()
        {
            var tx = new Transaction();

            tx.AddInput(this.GetRandomTx(), 0);
            tx.Inputs.First().PrevOut = new OutPoint(uint256.One, 11);

            tx.AddOutput(0, new Script());
            tx.AddOutput(1000, new Script(RandomUtils.GetBytes(50)));

            Assert.True(tx.IsCoinStake);

            return tx;
        }
    }
}
