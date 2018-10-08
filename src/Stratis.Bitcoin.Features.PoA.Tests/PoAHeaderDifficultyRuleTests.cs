using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Features.PoA.ConsensusRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class PoAHeaderDifficultyRuleTests
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
            BigInteger pow256 = BigInteger.ValueOf(2).Pow(256);
            Target defaultTarget = PoAHeaderDifficultyRule.PoABlockDifficulty;
            int headersCount = 200;

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(headersCount, null, false, defaultTarget);

            foreach (ChainedHeader chainedHeader in headers)
                chainedHeader.InvokeMethod("CalculateChainWork");

            foreach (ChainedHeader header in headers.Skip(2))
            {
                BigInteger chainworkDiff = new BigInteger(header.ChainWork.ToString()).Subtract(new BigInteger(header.Previous.ChainWork.ToString()));
                BigInteger chainworkDiffPrev = new BigInteger(header.Previous.ChainWork.ToString()).Subtract(new BigInteger(header.Previous.Previous.ChainWork.ToString()));

                Assert.Equal(chainworkDiff, chainworkDiffPrev);
            }
        }
    }
}
