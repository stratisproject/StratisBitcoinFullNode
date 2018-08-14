using System.Collections.Generic;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public sealed class ReflectionRuleRegistration : IRuleRegistration
    {
        public ICollection<IBaseConsensusRule> GetRules()
        {
            var rules = new List<IBaseConsensusRule>
            {
                new SmartContractFormatRule()
            };

            return rules;
        }
    }
}