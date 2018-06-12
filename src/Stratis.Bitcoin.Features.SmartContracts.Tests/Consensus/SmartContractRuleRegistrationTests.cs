using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus
{
    public class SmartContractRuleRegistrationTests
    {
        [Fact]
        public void SmartContractRuleRegistration_AddTests_Success()
        {
            var baseRuleRegistration = new Mock<IRuleRegistration>();

            var mockRules = new List<ConsensusRule>
            {
                new Mock<ConsensusRule>().Object,
                new Mock<ConsensusRule>().Object,
                new Mock<ConsensusRule>().Object
            };

            baseRuleRegistration.Setup(x => x.GetRules()).Returns(() => mockRules);

            var fullNodeBuilder = new Mock<IFullNodeBuilder>();
            fullNodeBuilder.SetupGet(f => f.ServiceProvider).Returns(new MockServiceProvider());

            var smartContractRuleRegistration = new SmartContractRuleRegistration(fullNodeBuilder.Object);
            smartContractRuleRegistration.SetPreviousRegistration(baseRuleRegistration.Object);

            var smartContractConsensusRules = smartContractRuleRegistration.GetRules().ToList();

            // Check that new rules are present
            Assert.Single(smartContractConsensusRules.OfType<TxOutSmartContractExecRule>());
            Assert.Single(smartContractConsensusRules.OfType<OpSpendRule>());
            Assert.Single(smartContractConsensusRules.OfType<SmartContractCoinviewRule>());

            // Check that original rules are present
            foreach (ConsensusRule rule in baseRuleRegistration.Object.GetRules())
            {
                Assert.Contains(rule, smartContractConsensusRules);
            }
        }
    }

    public sealed class MockServiceProvider : IServiceProvider
    {
        private readonly CoinView coinView;
        private readonly ISmartContractExecutorFactory executorFactory;
        private readonly ContractStateRepositoryRoot originalStateRoot;

        public MockServiceProvider()
        {
            this.coinView = new Mock<CoinView>().Object;
            this.executorFactory = new Mock<ISmartContractExecutorFactory>().Object;
            this.originalStateRoot = new Mock<ContractStateRepositoryRoot>().Object;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(CoinView))
                return this.coinView;

            if (serviceType == typeof(ISmartContractExecutorFactory))
                return this.executorFactory;

            if (serviceType == typeof(ContractStateRepositoryRoot))
                return this.originalStateRoot;

            return null;
        }
    }
}