using System.Collections.Generic;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Tests.Common
{
    public class TestRuleRegistration : IRuleRegistration
    {
        public TestRuleRegistration(IRuleRegistration existing)
        {
            var rules = existing.CreateRules();
            this.HeaderValidationRules = new List<IHeaderValidationConsensusRule>(rules.HeaderValidationRules);
            this.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>(rules.IntegrityValidationRules);
            this.PartialValidationRules = new List<IPartialValidationConsensusRule>(rules.PartialValidationRules);
            this.FullValidationRules = new List<IFullValidationConsensusRule>(rules.FullValidationRules);
        }

        public TestRuleRegistration()
        {
            this.HeaderValidationRules = new List<IHeaderValidationConsensusRule>();
            this.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>();
            this.PartialValidationRules = new List<IPartialValidationConsensusRule>();
            this.FullValidationRules = new List<IFullValidationConsensusRule>();
        }

        public List<IHeaderValidationConsensusRule> HeaderValidationRules { get; }
        public List<IIntegrityValidationConsensusRule> IntegrityValidationRules { get; }
        public List<IPartialValidationConsensusRule> PartialValidationRules { get; }
        public List<IFullValidationConsensusRule> FullValidationRules { get; }

        public RuleContainer CreateRules()
        {
            return new RuleContainer(this.FullValidationRules, this.PartialValidationRules, this.HeaderValidationRules, this.IntegrityValidationRules);
        }
    }
}