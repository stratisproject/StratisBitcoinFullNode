using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
        /// Whether the rule will be considered a rule that only does validation and does not manipulate state in any way.
        /// When <c>true</c> rule is allowed to skip validation when the <see cref="BlockValidationContext.SkipValidation"/> is set to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// State in this context is the manipulation of information in the consensus data store based on actions specified <see cref="Block"/> and <see cref="Transaction"/>.
        /// This will allow to ability to run validation checks on blocks (during mining for example) without change the underline store.
        /// </remarks>
        public virtual bool ValidationRule => true;

        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will throw.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public abstract Task RunAsync(ContextInformation context);
    }

    /// <summary>
    /// Rules that are manipulating state.
    /// </summary>
    public abstract class ExecutionConsensusRule : ConsensusRule
    {
        /// <inheritdoc />
        public override bool ValidationRule => false;
    }
}