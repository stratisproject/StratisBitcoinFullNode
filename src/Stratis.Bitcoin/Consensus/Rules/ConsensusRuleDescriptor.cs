using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Rules
{
    /// <summary>
    /// Provide a mapping between a rule and its <see cref="RuleAttribute"/> that can be used by the rule engine.
    /// </summary>
    public class ConsensusRuleDescriptor
    {
        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public ConsensusRuleDescriptor(ConsensusRule rule, RuleAttribute attribute)
        {
            Guard.NotNull(rule, nameof(rule));

            this.Rule = rule;
            this.RuleAttribute = attribute;
        }

        /// <summary>The rule represented by this descriptor.</summary>
        public ConsensusRule Rule { get; }

        /// <summary>The collection of <see cref="RuleAttribute"/> that are attached to this rule.</summary>
        public RuleAttribute RuleAttribute { get; }
    }
}