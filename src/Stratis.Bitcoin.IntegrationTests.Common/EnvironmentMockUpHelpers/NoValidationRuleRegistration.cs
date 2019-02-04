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
                new List<IFullValidationConsensusRule>(),
                new List<IPartialValidationConsensusRule>(),
                new List<IHeaderValidationConsensusRule>(),
                new List<IIntegrityValidationConsensusRule>()
            );
        }
    }
}