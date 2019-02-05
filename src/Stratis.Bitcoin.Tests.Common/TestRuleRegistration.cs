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
            this.HeaderValidationRules = new List<HeaderValidationConsensusRule>(rules.HeaderValidationRules);
            this.IntegrityValidationRules = new List<IntegrityValidationConsensusRule>(rules.IntegrityValidationRules);
            this.PartialValidationRules = new List<PartialValidationConsensusRule>(rules.PartialValidationRules);
            this.FullValidationRules = new List<FullValidationConsensusRule>(rules.FullValidationRules);
        }

        public TestRuleRegistration()
        {
            this.HeaderValidationRules = new List<HeaderValidationConsensusRule>();
            this.IntegrityValidationRules = new List<IntegrityValidationConsensusRule>();
            this.PartialValidationRules = new List<PartialValidationConsensusRule>();
            this.FullValidationRules = new List<FullValidationConsensusRule>();
        }

        public List<HeaderValidationConsensusRule> HeaderValidationRules { get; }
        public List<IntegrityValidationConsensusRule> IntegrityValidationRules { get; }
        public List<PartialValidationConsensusRule> PartialValidationRules { get; }
        public List<FullValidationConsensusRule> FullValidationRules { get; }

        public RuleContainer CreateRules()
        {
            return new RuleContainer(this.FullValidationRules, this.PartialValidationRules, this.HeaderValidationRules, this.IntegrityValidationRules);
        }
    }
}