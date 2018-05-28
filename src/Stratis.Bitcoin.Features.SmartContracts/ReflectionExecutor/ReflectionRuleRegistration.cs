using System.Collections.Generic;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public class ReflectionRuleRegistration : IAdditionalRuleRegistration
    {
        private IRuleRegistration baseRuleRegistration;

        public void SetPreviousRegistration(IRuleRegistration previousRegistration)
        {
            this.baseRuleRegistration = previousRegistration;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            var rules = new List<ConsensusRule>();
            rules.AddRange(this.baseRuleRegistration.GetRules());
            rules.Add(new SmartContractFormatRule());
            return rules;
        }
    }
}
