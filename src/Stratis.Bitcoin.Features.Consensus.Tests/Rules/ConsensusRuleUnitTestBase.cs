using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class ConsensusRuleUnitTestBase
    {
        protected Network network;
        protected Mock<ILoggerFactory> loggerFactory;
        protected Mock<IDateTimeProvider> dateTimeProvider;
        protected ConcurrentChain concurrentChain;
        protected NodeDeployments nodeDeployments;
        protected ConsensusSettings consensusSettings;
        protected Mock<ICheckpoints> checkpoints;
        protected List<ConsensusRule> ruleRegistrations;
        protected Mock<IRuleRegistration> ruleRegistration;
        protected TestConsensusRules consensusRules;

        public ConsensusRuleUnitTestBase()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
            this.network = Network.TestNet;
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            this.dateTimeProvider = new Mock<IDateTimeProvider>();

            this.concurrentChain = new ConcurrentChain(this.network);
            this.nodeDeployments = new NodeDeployments(this.network, this.concurrentChain);
            this.consensusSettings = new ConsensusSettings();
            this.checkpoints = new Mock<ICheckpoints>();

            this.ruleRegistrations = new List<ConsensusRule>();
            this.ruleRegistration = new Mock<IRuleRegistration>();
            this.ruleRegistration.Setup(r => r.GetRules())
                .Returns(() => { return this.ruleRegistrations; });

            this.consensusRules = InitializeConsensusRules();
        }

        public TestConsensusRules InitializeConsensusRules()
        {
            return new TestConsensusRules(this.network, this.loggerFactory.Object, this.dateTimeProvider.Object, this.concurrentChain, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object);
        }

    }
}
