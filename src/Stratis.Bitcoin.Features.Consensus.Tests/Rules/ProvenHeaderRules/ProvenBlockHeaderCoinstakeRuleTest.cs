using System;
using System.Threading;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.ProvenHeaderRules
{
    public class ProvenBlockHeaderCoinstakeRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private readonly PosConsensusOptions options;
        private readonly Mock<IStakeValidator> stakeValidatorMock = new Mock<IStakeValidator>();

        public ProvenBlockHeaderCoinstakeRuleTest()
        {
            this.options = (PosConsensusOptions)this.network.Consensus.Options;
        }

        [Fact]
        public void RunRule_ProvenHeadersNotActive_RuleIsSkipped()
        {
            // Setup proven header.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height below proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);

            // When we run the validation rule, we should not hit any exceptions as rule will be skipped.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().NotThrow();
        }

        [Fact]
        public void RunRule_ContextChainedHeaderIsNull_ArgumentNullExceptionIsThrown()
        {
            // Setup null chained header.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = null;

            // When we run the validation rule, we should hit nul argument exception for chained header.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void RunRule_ProvenHeadersActive_And_CoinstakeIsNull_EmptyCoinstakeErrorIsThrown()
        {
            // Setup proven header.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.options.ProvenHeadersActivationHeight + 10);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.SetPrivateVariableValue<Transaction>("coinstake", null);

            // When we run the validation rule, we should hit coinstake empty exception.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                          .And.ConsensusError
                          .Should().Be(ConsensusErrors.EmptyCoinstake);
        }

        [Fact]
        public void RunRule_ProvenHeadersActive_And_CoinstakeUtxoIsEmpty_ReadTxPrevFailedErrorIsThrown()
        {
            // Setup proven header.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height higher than proven header activation height.
            // By default no utxo are setup in coinview so fetch we return nothing.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.options.ProvenHeadersActivationHeight + 10);

            // When we run the validation rule, we should hit coinstake read transaction error.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                .And.ConsensusError
                .Should().Be(ConsensusErrors.ReadTxPrevFailed);
        }

        [Fact]
        public void RunRule_ProvenHeadersActive_And_CoinstakeUnspentOutputsAreIncorrect_ReadTxPrevFailedErrorIsThrown()
        {
            // Setup proven header.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.options.ProvenHeadersActivationHeight + 10);
            
            // Add more than one unspent output to coinstake.
            this.coinView
                .Setup(m => m.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FetchCoinsResponse(new [] { new UnspentOutputs(10, new Transaction()), new UnspentOutputs(11, new Transaction()) }, posBlock.GetHash()));

            // When we run the validation rule, we should hit coinstake read transaction error.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                .And.ConsensusError
                .Should().Be(ConsensusErrors.ReadTxPrevFailed);
        }

        [Fact]
        public void RunRule_ProvenHeadersActive_And_CoinstakeUnspentOutputsIsNull_ReadTxPrevFailedErrorIsThrown()
        {
            // Setup proven header.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.options.ProvenHeadersActivationHeight + 10);

            // Add more null unspent output to coinstake.
            this.coinView
                .Setup(m => m.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FetchCoinsResponse(new[] { (UnspentOutputs)null }, posBlock.GetHash()));

            // When we run the validation rule, we should hit coinstake read transaction error.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                .And.ConsensusError
                .Should().Be(ConsensusErrors.ReadTxPrevFailed);
        }

        [Fact]
        public void RunRule_ProvenHeadersActive_And_CoinstakeIsIncorrectlySetup_NonCoinstakeErrorIsThrown()
        {
            // Setup proven header.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.options.ProvenHeadersActivationHeight + 10);

            // Setup coinstake transaction.
            this.coinView
                .Setup(m => m.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FetchCoinsResponse(new[] { new UnspentOutputs(10, new Transaction()) }, posBlock.GetHash()));

            // Change coinstake outputs to make it invalid.
            ((ProvenBlockHeader)this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header).Coinstake.Outputs.RemoveAt(0);

            // When we run the validation rule, we should hit coinstake read transaction error.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                .And.ConsensusError
                .Should().Be(ConsensusErrors.NonCoinstake);
        }

        [Fact]
        public void RunRule_ProvenHeadersActive_And_InvalidStakeTime_StakeTimeViolationErrorIsThrown()
        {
            // Setup proven header with null coinstake.
            PosBlock posBlock = new PosBlockBuilder(this.network).Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.options.ProvenHeadersActivationHeight + 10);

            // Setup coinstake transaction.
            this.coinView
                .Setup(m => m.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FetchCoinsResponse(new[] { new UnspentOutputs(10, new Transaction()) }, posBlock.GetHash()));

            // Change coinstake time to differ from header time but divisible by 16.
            ((ProvenBlockHeader)this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header).Coinstake.Time = 16;

            // When we run the validation rule, we should hit coinstake read transaction error.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderCoinstakeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                .And.ConsensusError
                .Should().Be(ConsensusErrors.StakeTimeViolation);

            // Change coinstake time to be the same asot header time but not divisible by 16.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = 50;
            ((ProvenBlockHeader)this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header).Coinstake.Time = 50;

            // When we run the validation rule, we should hit coinstake read transaction error.
            ruleValidation.Should().Throw<ConsensusErrorException>()
                .And.ConsensusError
                .Should().Be(ConsensusErrors.StakeTimeViolation);
        }
    }
}
