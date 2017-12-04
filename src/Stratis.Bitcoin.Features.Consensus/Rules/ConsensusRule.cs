using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// An abstract rule for allowing to write consensus rules.
    /// </summary>
    public abstract class ConsensusRule
    {
        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; set; }

        /// <summary>The engine this rule belongs to.</summary>
        public ConsensusRules Parent { get; set; }

        /// <summary>
        /// A collection of rules that the current rule depends on.
        /// </summary>
        /// <returns>A collection of rules this rule depends on.</returns>
        public virtual IEnumerable<Type> Dependencies()
        {
            return Enumerable.Empty<Type>();
        }

        /// <summary>
        /// An indicator whether this rule is allowed to skip validation when the <see cref="BlockValidationContext.SkipValidation"/> is set to <c>true</c>.
        /// </summary>
        public virtual bool CanSkipValidation => false;

        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will throw.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public abstract Task RunAsync(ContextInformation context);
    }

    /// <summary>
    /// Rules that override this base will always allow to skip validation.
    /// The property <see cref="ConsensusRule.CanSkipValidation"/> will default to true.
    /// </summary>
    public abstract class SkipValidationConsensusRule : ConsensusRule
    {
        /// <inheritdoc />
        public override bool CanSkipValidation => true;

        /// <inheritdoc />
        public override IEnumerable<Type> Dependencies()
        {
            yield return typeof(BlockPreviousHeaderRule);
        }
    }
}