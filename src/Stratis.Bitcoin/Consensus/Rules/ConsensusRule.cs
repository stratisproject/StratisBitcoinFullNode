using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Rules;

namespace Stratis.Bitcoin.Consensus.Rules
{
    public abstract class ConsensusRuleBase : IConsensusRuleBase
    {
        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; set; }

        /// <summary>The engine this rule belongs to.</summary>
        public ConsensusRuleEngine Parent { get; set; }

        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        public abstract void Run(RuleContext context);

        /// <summary>
        /// Allow a rule to initialize itself.
        /// The rule can verify that other rules are present using the <see cref="IConsensusRuleEngine.Rules"/>.
        /// The rule can internally initialize its state.
        /// </summary>
        public virtual void Initialize()
        {
        }
    }

    public abstract class HeaderValidationConsensusRule : ConsensusRuleBase, IHeaderValidationConsensusRule
    {
    }

    public abstract class IntegrityValidationConsensusRule : ConsensusRuleBase, IIntegrityValidationConsensusRule
    {
    }

    public abstract class PartialValidationConsensusRule : ConsensusRuleBase, IPartialValidationConsensusRule
    {
    }

    public abstract class FullValidationConsensusRule : ConsensusRuleBase, IFullValidationConsensusRule
    {
    }
}