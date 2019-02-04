using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;

namespace Stratis.Bitcoin.Consensus.Rules
{
    /// <summary>
    /// An interface that will allow the registration of bulk consensus rules in to the engine.
    /// </summary>
    /// <remarks>
    /// It is important to note that there is high importance to the order the rules are registered
    /// with the engine, this is important for rules with dependencies on other rules.
    /// Rules are executed in the same order they are registered with the engine.
    /// </remarks>
    public interface IRuleRegistration
    {
        RuleContainer CreateRules();
    }

    public class RuleContainer
    {
        public RuleContainer(
            IReadOnlyList<IFullValidationConsensusRule> fullValidationConsensusRules,
            IReadOnlyList<IPartialValidationConsensusRule> partialValidationConsensusRules,
            IReadOnlyList<IHeaderValidationConsensusRule> headerValidationConsensusRules,
            IReadOnlyList<IIntegrityValidationConsensusRule> integrityValidationConsensusRules
            )
        {
            this.FullValidationRules = fullValidationConsensusRules;
            this.PartialValidationRules = partialValidationConsensusRules;
            this.HeaderValidationRules = headerValidationConsensusRules;
            this.IntegrityValidationRules = integrityValidationConsensusRules;
        }

        public IReadOnlyList<IHeaderValidationConsensusRule> HeaderValidationRules { get; }
        public IReadOnlyList<IIntegrityValidationConsensusRule> IntegrityValidationRules { get; }
        public IReadOnlyList<IPartialValidationConsensusRule> PartialValidationRules { get; }
        public IReadOnlyList<IFullValidationConsensusRule> FullValidationRules { get; }
    }
}