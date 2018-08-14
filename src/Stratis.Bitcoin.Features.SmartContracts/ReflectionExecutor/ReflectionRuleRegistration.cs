using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public sealed class ReflectionRuleRegistration : IRuleRegistration
    {
        public void RegisterRules(IConsensus consensus)
        {
            consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
            {
                new SmartContractFormatRule()
            };
        }
    }
}