using System.Collections.Generic;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public sealed class ReflectionRuleRegistration : IRuleRegistration
    {
        public IEnumerable<ConsensusRule> GetRules()
        {
            var rules = new List<ConsensusRule>
            {
                new SmartContractFormatRule()
            };

            return rules;
        }
    }
}