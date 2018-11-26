using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.ProvenHeaderRules
{
    public class ProvenBlockHeaderSizeRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private PosConsensusOptions options;
        private int provenHeadersActivationHeight;

        public ProvenBlockHeaderSizeRuleTest()
        {
            this.options = (PosConsensusOptions)this.network.Consensus.Options;
            this.provenHeadersActivationHeight = this.network.Checkpoints.Keys.Last();
        }

        [Fact]
        public void RunRule_OversizedMerkleProof_And_ProvenHeadersActive_ThrowsBadProvenHeaderMerkleProofSize()
        {
            // Setup proven header with invalid merkle proof (larger than 512).
            PosBlock posBlock = new PosBlockBuilder(this.network).WithLargeMerkleProof().Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();
            provenBlockHeader.ToBytes(); // this will trigger serialization to get the PH size.

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.provenHeadersActivationHeight + 10);

            // When we run the validation rule, we should hit merkle proof validation exception.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderSizeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                          .And.ConsensusError
                          .Should().Be(ConsensusErrors.BadProvenHeaderMerkleProofSize);
        }

        [Fact]
        public void RunRule_OversizedMerkleProof_And_ProvenHeadersNotActive_RuleIsSkipped()
        {
            // Setup proven header with invalid merkle proof (larger than 512 bytes).
            PosBlock posBlock = new PosBlockBuilder(this.network).WithLargeMerkleProof().Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height below proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);

            // When we run the validation rule, we should not hit any exceptions as rule will be skipped.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderSizeRule>().Run(this.ruleContext);
            ruleValidation.Should().NotThrow();
        }

        [Fact]
        public void RunRule_OversizedCoinstake_And_ProvenHeadersActive_ThrowsBadProvenHeaderCoinstakeSize()
        {
            // Setup proven header with invalid coinstake (larger than 1,000,000 bytes).
            PosBlock posBlock = new PosBlockBuilder(this.network).WithLargeCoinstake().Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();
            provenBlockHeader.ToBytes(); // this will trigger serialization to get the PH size.

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.provenHeadersActivationHeight + 10);

            // When we run the validation rule, we should hit merkle proof validation exception.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderSizeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                          .And.ConsensusError
                          .Should().Be(ConsensusErrors.BadProvenHeaderCoinstakeSize);
        }

        [Fact]
        public void RunRule_OversizedCoinstake_And_ProvenHeadersNotActive_RuleIsSkipped()
        {
            // Setup proven header with invalid coinstake (larger than 1,000,000).
            PosBlock posBlock = new PosBlockBuilder(this.network).WithLargeCoinstake().Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height below proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);

            // When we run the validation rule, we should not hit any exceptions as rule will be skipped.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderSizeRule>().Run(this.ruleContext);
            ruleValidation.Should().NotThrow();
        }

        [Fact]
        public void RunRule_OversizedSignature_And_ProvenHeadersActive_ThrowsBadProvenHeaderCoinstakeSize()
        {
            // Setup proven header with invalid signature (larger than 80 bytes).
            PosBlock posBlock = new PosBlockBuilder(this.network).WithLargeSignature().Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();
            provenBlockHeader.ToBytes(); // this will trigger serialization to get the PH size.

            // Setup chained header and move it to the height higher than proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.SetPrivatePropertyValue("Height", this.provenHeadersActivationHeight + 10);

            // When we run the validation rule, we should hit merkle proof validation exception.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderSizeRule>().Run(this.ruleContext);
            ruleValidation.Should().Throw<ConsensusErrorException>()
                          .And.ConsensusError
                          .Should().Be(ConsensusErrors.BadProvenHeaderSignatureSize);
        }

        [Fact]
        public void RunRule_OversizedSignature_And_ProvenHeadersNotActive_RuleIsSkipped()
        {
            // Setup proven header with invalid signature (larger than 80 bytes).
            PosBlock posBlock = new PosBlockBuilder(this.network).WithLargeSignature().Build();
            ProvenBlockHeader provenBlockHeader = new ProvenBlockHeaderBuilder(posBlock, this.network).Build();

            // Setup chained header and move it to the height below proven header activation height.
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(provenBlockHeader, provenBlockHeader.GetHash(), null);

            // When we run the validation rule, we should not hit any exceptions as rule will be skipped.
            Action ruleValidation = () => this.consensusRules.RegisterRule<ProvenHeaderSizeRule>().Run(this.ruleContext);
            ruleValidation.Should().NotThrow();
        }
    }
}
