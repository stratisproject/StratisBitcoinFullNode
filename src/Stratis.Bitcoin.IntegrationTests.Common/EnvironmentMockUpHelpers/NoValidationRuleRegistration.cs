using System.Collections.Generic;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class NoValidationRuleRegistration : IRuleRegistration
    {
        public RuleContainer CreateRules()
        {
            return new RuleContainer(
                new List<FullValidationConsensusRule>(),
                new List<PartialValidationConsensusRule>(),
                new List<HeaderValidationConsensusRule>(),
                new List<IntegrityValidationConsensusRule>()
            );
        }
    }
}