using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.ConsensusRules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class HeaderTimeChecksPoARuleTests
    {
        private readonly HeaderTimeChecksPoARule timeChecksRule;

        private readonly ChainedHeader currentHeader;
        private readonly ChainedHeader prevHeader;

        private readonly PoANetwork network;

        public HeaderTimeChecksPoARuleTests()
        {
            var loggerFactory = new LoggerFactory();
            this.network = new PoANetwork();
            this.timeChecksRule = new HeaderTimeChecksPoARule();
            var chain = new ConcurrentChain(this.network);
            IDateTimeProvider timeProvider = new DateTimeProvider();
            var consensusSettings = new ConsensusSettings(NodeSettings.Default());

            var powConsensusRulesEngine = new PowConsensusRuleEngine(this.network, loggerFactory, new DateTimeProvider(), chain,
                new NodeDeployments(this.network, chain), consensusSettings, new Checkpoints(this.network, consensusSettings), new Mock<ICoinView>().Object,
                new ChainState(), new InvalidBlockHashStore(timeProvider), new NodeStats(timeProvider));

            this.timeChecksRule.Parent = powConsensusRulesEngine;
            this.timeChecksRule.Logger = loggerFactory.CreateLogger(this.timeChecksRule.GetType().FullName);
            this.timeChecksRule.Initialize();

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(50, null, false);

            this.currentHeader = headers.Last();
            this.prevHeader = this.currentHeader.Previous;
        }

        [Fact]
        public void EnsureTimestampOfNextBlockIsGreaterThanPrevBlock()
        {
            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, DateTimeOffset.Now);

            // New block has smaller timestamp.
            this.currentHeader.Header.Time = 100;
            this.prevHeader.Header.Time = 101;

            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            try
            {
                this.timeChecksRule.Run(ruleContext);
            }
            catch (ConsensusErrorException exception)
            {
                Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
            }

            // New block has equal timestamp.
            this.prevHeader.Header.Time = 100;
            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            // New block has greater timestamp.
            this.prevHeader.Header.Time = 80;
            this.timeChecksRule.Run(ruleContext);
        }

        [Fact]
        public void EnsureTimestampIsNotTooNew()
        {
            var time = new DateTimeOffset(DateTime.UtcNow);

            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, time);

            // New block has smaller timestamp.
            this.prevHeader.Header.Time = (uint)time.ToUnixTimeSeconds();

            this.currentHeader.Header.Time = this.prevHeader.Header.Time + this.network.TargetSpacingSeconds - 1;
            this.timeChecksRule.Run(ruleContext);

            this.currentHeader.Header.Time = this.prevHeader.Header.Time + this.network.TargetSpacingSeconds;
            this.timeChecksRule.Run(ruleContext);

            this.currentHeader.Header.Time = this.prevHeader.Header.Time + 1;
            this.timeChecksRule.Run(ruleContext);

            this.currentHeader.Header.Time = this.prevHeader.Header.Time + this.network.TargetSpacingSeconds + 1;
            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            try
            {
                this.timeChecksRule.Run(ruleContext);
            }
            catch (ConsensusErrorException exception)
            {
                Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
            }
        }
    }
}
