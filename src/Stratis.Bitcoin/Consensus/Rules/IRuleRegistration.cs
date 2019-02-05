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
            IReadOnlyList<FullValidationConsensusRule> fullValidationConsensusRules,
            IReadOnlyList<PartialValidationConsensusRule> partialValidationConsensusRules,
            IReadOnlyList<HeaderValidationConsensusRule> headerValidationConsensusRules,
            IReadOnlyList<IntegrityValidationConsensusRule> integrityValidationConsensusRules
            )
        {
            this.FullValidationRules = fullValidationConsensusRules;
            this.PartialValidationRules = partialValidationConsensusRules;
            this.HeaderValidationRules = headerValidationConsensusRules;
            this.IntegrityValidationRules = integrityValidationConsensusRules;
        }

        /// <summary>Group of rules that are used during block header validation specific to the given network.</summary>
        public IReadOnlyList<HeaderValidationConsensusRule> HeaderValidationRules { get; }

        /// <summary>Group of rules that are used during block integrity validation specific to the given network.</summary>
        public IReadOnlyList<IntegrityValidationConsensusRule> IntegrityValidationRules { get; }

        /// <summary>Group of rules that are used during partial block validation specific to the given network.</summary>
        public IReadOnlyList<PartialValidationConsensusRule> PartialValidationRules { get; }

        /// <summary>Group of rules that are used during full validation (connection of a new block) specific to the given network.</summary>
        public IReadOnlyList<FullValidationConsensusRule> FullValidationRules { get; }
    }
}