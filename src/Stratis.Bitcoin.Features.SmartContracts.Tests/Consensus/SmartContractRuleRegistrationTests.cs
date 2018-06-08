using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
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
            Assert.Single(smartContractConsensusRules.OfType<OpCreateZeroValueRule>());
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
        private readonly Dictionary<Type, object> registered;

        public MockServiceProvider()
        {
            this.registered = new Dictionary<Type, object>
            {
                { typeof(CoinView), new Mock<CoinView>().Object },
                { typeof(ISmartContractExecutorFactory), new Mock<ISmartContractExecutorFactory>().Object },
                { typeof(ContractStateRepositoryRoot), new Mock<ContractStateRepositoryRoot>().Object },
                { typeof(ILoggerFactory), new Mock<ILoggerFactory>().Object },
                { typeof(ISmartContractReceiptStorage), new Mock<ISmartContractReceiptStorage>().Object }
            };
        }

        public object GetService(Type serviceType)
        {
            return this.registered[serviceType];
        }
    }
}