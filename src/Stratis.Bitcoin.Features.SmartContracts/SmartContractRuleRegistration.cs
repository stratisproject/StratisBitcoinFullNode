using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractRuleRegistration : IRuleRegistration
    {
        private readonly IRuleRegistration baseRuleRegistration;

        public SmartContractRuleRegistration(IRuleRegistration baseRuleRegistration)
        {
            this.baseRuleRegistration = baseRuleRegistration;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            foreach (ConsensusRule rule in this.baseRuleRegistration.GetRules())
            {
                yield return rule;
            }

            yield return new TxOutSmartContractExecRule();
            yield return new OpSpendRule();
            yield return new GasBudgetRule();
        }
    }
}
