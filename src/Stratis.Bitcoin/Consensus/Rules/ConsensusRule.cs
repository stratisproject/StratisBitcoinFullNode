using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Rules;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus.Rules
{
    public class ConsensusRulesContainer
    {
        /// <summary>Group of rules that are used during block header validation.</summary>
        public List<HeaderValidationConsensusRule> HeaderValidationRules { get; set; }

        /// <summary>Group of rules that are used during block integrity validation.</summary>
        public List<IntegrityValidationConsensusRule> IntegrityValidationRules { get; set; }

        /// <summary>Group of rules that are used during partial block validation.</summary>
        public List<PartialValidationConsensusRule> PartialValidationRules { get; set; }

        /// <summary>Group of rules that are used during full validation (connection of a new block).</summary>
        public List<FullValidationConsensusRule> FullValidationRules { get; set; }

        public ConsensusRulesContainer()
        {
            this.HeaderValidationRules = new List<HeaderValidationConsensusRule>();
            this.IntegrityValidationRules = new List<IntegrityValidationConsensusRule>();
            this.PartialValidationRules = new List<PartialValidationConsensusRule>();
            this.FullValidationRules = new List<FullValidationConsensusRule>();
        }

        public ConsensusRulesContainer(
            IEnumerable<IHeaderValidationConsensusRule> headerValidationConsensusRules,
            IEnumerable<IIntegrityValidationConsensusRule> integrityValidationConsensusRules,
            IEnumerable<IPartialValidationConsensusRule> partialValidationConsensusRules,
            IEnumerable<IFullValidationConsensusRule> fullValidationConsensusRules)
        {
            this.HeaderValidationRules = headerValidationConsensusRules.OfType<HeaderValidationConsensusRule>().ToList();
            this.IntegrityValidationRules = integrityValidationConsensusRules.OfType<IntegrityValidationConsensusRule>().ToList();
            this.PartialValidationRules = partialValidationConsensusRules.OfType<PartialValidationConsensusRule>().ToList();
            this.FullValidationRules = fullValidationConsensusRules.OfType<FullValidationConsensusRule>().ToList();
        }
    }

    public abstract class ConsensusRuleBase : IConsensusRuleBase
    {
        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; set; }

        /// <summary>The engine this rule belongs to.</summary>
        public ConsensusRuleEngine Parent { get; set; }

        /// <summary>
        /// Allow a rule to initialize itself.
        /// The rule can verify that other rules are present using the <see cref="IConsensusRuleEngine.Rules"/>.
        /// The rule can internally initialize its state.
        /// </summary>
        [NoTrace]
        public virtual void Initialize()
        {
        }
    }

    /// <summary>An abstract rule for implementing consensus rules.</summary>
    public abstract class SyncConsensusRule : ConsensusRuleBase
    {
        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        public abstract void Run(RuleContext context);
    }

    /// <summary>An abstract rule for implementing consensus rules.</summary>
    public abstract class AsyncConsensusRule : ConsensusRuleBase
    {
        /// <summary>
        /// Execute the logic in the current rule in an async approach.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public abstract Task RunAsync(RuleContext context);
    }

    public abstract class HeaderValidationConsensusRule : SyncConsensusRule, IHeaderValidationConsensusRule
    {
    }

    public abstract class IntegrityValidationConsensusRule : SyncConsensusRule, IIntegrityValidationConsensusRule
    {
    }

    public abstract class PartialValidationConsensusRule : AsyncConsensusRule, IPartialValidationConsensusRule
    {
    }

    public abstract class FullValidationConsensusRule : AsyncConsensusRule, IFullValidationConsensusRule
    {
    }
}