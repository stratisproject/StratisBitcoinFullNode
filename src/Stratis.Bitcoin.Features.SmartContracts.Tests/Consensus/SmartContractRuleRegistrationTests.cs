using System.Collections.Generic;
using System.Linq;
using Moq;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
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

            var smartContractRuleRegistration = new SmartContractRuleRegistration(baseRuleRegistration.Object);

            var smartContractConsensusRules = smartContractRuleRegistration.GetRules().ToList();

            // Check that new rules are present
            Assert.Single(smartContractConsensusRules.OfType<TxOutSmartContractExecRule>());
            Assert.Single(smartContractConsensusRules.OfType<OpSpendRule>());
            Assert.Single(smartContractConsensusRules.OfType<GasBudgetRule>());
            Assert.Single(smartContractConsensusRules.OfType<OpCreateZeroValueRule>());

            // Check that original rules are present
            foreach (ConsensusRule rule in baseRuleRegistration.Object.GetRules())
            {
                Assert.Contains(rule, smartContractConsensusRules);
            }
        }
    }
}