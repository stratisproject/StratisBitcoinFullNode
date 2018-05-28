using System.Collections.Generic;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractRuleRegistration : IRuleRegistration
    {
        private readonly IRuleRegistration baseRuleRegistration;

        public SmartContractRuleRegistration(IRuleRegistration baseRuleRegistration)
        {
            this.baseRuleRegistration = baseRuleRegistration;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            var rules = new List<ConsensusRule>();
            rules.AddRange(this.baseRuleRegistration.GetRules());
            rules.Add(new TxOutSmartContractExecRule());
            rules.Add(new OpSpendRule());
            //rules.Add(new GasBudgetRule());
            rules.Add(new OpCreateZeroValueRule());
            return rules;
        }
    }
}