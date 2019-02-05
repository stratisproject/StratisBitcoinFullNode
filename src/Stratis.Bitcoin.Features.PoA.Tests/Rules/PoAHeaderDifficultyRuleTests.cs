using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class PoAHeaderDifficultyRuleTests : PoATestsBase
    {
        [Fact]
        public void CumulativeWorkForALotOfBlocksIsLowerThanMaxValue()
        {
            Target defaultTarget = PoAHeaderDifficultyRule.PoABlockDifficulty;
            int times = 110_000_000;

            long result = defaultTarget.ToCompact() * times;

            Assert.True(result < long.MaxValue);
        }

        [Fact]
        public void ChainworkIsIncreasedBySameConstantValue()
        {
            Target defaultTarget = PoAHeaderDifficultyRule.PoABlockDifficulty;
            int headersCount = 100;

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(headersCount, null, false, defaultTarget);

            foreach (ChainedHeader header in headers.Skip(2))
            {
                var currentHeaderWork = new BigInteger(header.ChainWork.ToBytes());
                var prevHeaderWork = new BigInteger(header.Previous.ChainWork.ToBytes());

                BigInteger chainworkDiff = currentHeaderWork.Subtract(prevHeaderWork);
                BigInteger chainworkDiffPrev = new BigInteger(header.Previous.ChainWork.ToBytes()).Subtract(new BigInteger(header.Previous.Previous.ChainWork.ToBytes()));

                int comp = chainworkDiff.CompareTo(chainworkDiffPrev);
                Assert.True(comp == 0);
            }
        }

        [Fact]
        public void VerifyHeaderDifficulty()
        {
            var rule = new PoAHeaderDifficultyRule();
            this.InitRule(rule);

            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, DateTimeOffset.Now);

            this.currentHeader.Header.Bits = new Target(123);
            Assert.Throws<ConsensusErrorException>(() => rule.Run(ruleContext));

            this.currentHeader.Header.Bits = PoAHeaderDifficultyRule.PoABlockDifficulty;
            rule.Run(ruleContext);
        }
    }
}
