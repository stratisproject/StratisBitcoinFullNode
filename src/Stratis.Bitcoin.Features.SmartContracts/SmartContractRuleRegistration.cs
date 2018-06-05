using System.Collections.Generic;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractRuleRegistration : IAdditionalRuleRegistration
    {
        private IRuleRegistration baseRegistration;

        public void SetPreviousRegistration(IRuleRegistration previousRegistration)
        {
            this.baseRegistration = previousRegistration;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            Guard.Assert(this.baseRegistration != null);

            var rules = new List<ConsensusRule>();
            rules.AddRange(this.baseRegistration.GetRules());
            rules.Add(new TxOutSmartContractExecRule());
            rules.Add(new OpSpendRule());
            rules.Add(new OpCreateZeroValueRule());
            return rules;
        }
    }
}