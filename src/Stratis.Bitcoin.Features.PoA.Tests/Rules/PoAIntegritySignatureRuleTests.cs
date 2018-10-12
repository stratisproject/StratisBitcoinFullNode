﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.ConsensusRules;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class PoAIntegritySignatureRuleTests : PoARulesTestsBase
    {
        private readonly PoAIntegritySignatureRule integritySignatureRule;

        public PoAIntegritySignatureRuleTests()
        {
            this.integritySignatureRule = new PoAIntegritySignatureRule();
            this.integritySignatureRule.Parent = this.rulesEngine;
            this.integritySignatureRule.Logger = this.loggerFactory.CreateLogger(this.integritySignatureRule.GetType().FullName);
            this.integritySignatureRule.Initialize();
        }

        [Fact]
        public void CheckSignaturesEqual()
        {
            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader, BlockToValidate = block};
            var ruleContext = new RuleContext(validationContext, DateTimeOffset.Now);

            var tool = new KeyTool(new DataFolder(string.Empty));
            Key key1 = tool.GeneratePrivateKey();
            Key key2 = tool.GeneratePrivateKey();

            this.poaHeaderValidator.Sign(key1, this.currentHeader.Header as PoABlockHeader);
            this.poaHeaderValidator.Sign(key2, block.Header as PoABlockHeader);

            Assert.Throws<ConsensusErrorException>(() => this.integritySignatureRule.Run(ruleContext));

            (block.Header as PoABlockHeader).BlockSignature = (this.currentHeader.Header as PoABlockHeader).BlockSignature;

            this.integritySignatureRule.Run(ruleContext);
        }
    }
}
