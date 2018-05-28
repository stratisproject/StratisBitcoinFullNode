using System.Collections.Generic;
using System.Linq;
using Moq;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus
{
    public class ReflectionRuleRegistrationTests
    {
        [Fact]
        public void ReflectionRuleRegistration_AddTests_Success()
        {
            var baseRuleRegistration = new Mock<IRuleRegistration>();

            var mockRules = new List<ConsensusRule>
            {
                new Mock<ConsensusRule>().Object,
                new Mock<ConsensusRule>().Object,
                new Mock<ConsensusRule>().Object
            };

            baseRuleRegistration.Setup(x => x.GetRules()).Returns(() => mockRules);

            var reflectionRuleRegistration = new ReflectionRuleRegistration();
            reflectionRuleRegistration.SetPreviousRegistration(baseRuleRegistration.Object);

            var smartContractConsensusRules = reflectionRuleRegistration.GetRules().ToList();

            // Check that new rules are present
            Assert.Single(smartContractConsensusRules.OfType<SmartContractFormatRule>());

            // Check that original rules are present
            foreach (ConsensusRule rule in baseRuleRegistration.Object.GetRules())
            {
                Assert.Contains(rule, smartContractConsensusRules);
            }
        }
    }
}
