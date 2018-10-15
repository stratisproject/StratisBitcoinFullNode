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
    public class PoAHeaderSignatureRuleTests : PoARulesTestsBase
    {
        private readonly PoAHeaderSignatureRule signatureRule;

        private static Key key = new KeyTool(new DataFolder(string.Empty)).GeneratePrivateKey();

        public PoAHeaderSignatureRuleTests() : base(new TestPoANetwork(new List<PubKey>() { key.PubKey }))
        {
            this.signatureRule = new PoAHeaderSignatureRule();
            this.signatureRule.Parent = this.rulesEngine;
            this.signatureRule.Logger = this.loggerFactory.CreateLogger(this.signatureRule.GetType().FullName);
            this.signatureRule.Initialize();
        }

        [Fact]
        public void SignatureIsValidated()
        {
            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, DateTimeOffset.Now);

            Key randomKey = new KeyTool(new DataFolder(string.Empty)).GeneratePrivateKey();
            this.poaHeaderValidator.Sign(randomKey, this.currentHeader.Header as PoABlockHeader);

            Assert.Throws<ConsensusErrorException>(() => this.signatureRule.Run(ruleContext));

            this.poaHeaderValidator.Sign(key, this.currentHeader.Header as PoABlockHeader);

            this.signatureRule.Run(ruleContext);
        }
    }
}
