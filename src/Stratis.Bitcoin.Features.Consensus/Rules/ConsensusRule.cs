using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

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
        /// Allow a rule to initialize itself.
        /// The rule can verify that other rules are present using the <see cref="IConsensusRules.Rules"/>.
        /// The rule can internally initialize its state.
        /// </summary>
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will throw.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public abstract Task RunAsync(RuleContext context);
    }

    /// <summary>
    /// Provide additional information about a consensus rule that can be used by the rule engine.
    /// </summary>
    public class ConsensusRuleDescriptor
    {
        private readonly ValidationRule validationRule;

        public ConsensusRuleDescriptor(ConsensusRule rule)
        {
            this.Rule = rule;
            this.Attributes = Attribute.GetCustomAttributes(rule.GetType()).OfType<RuleAttribute>().ToList();

            this.validationRule = this.Attributes.OfType<ValidationRule>().FirstOrDefault();
        }

        public bool CanSkipValidation => this.validationRule?.CanSkipValidation ?? true;

        public ConsensusRule Rule { get; }

        public List<RuleAttribute> Attributes { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class RuleAttribute : Attribute
    {
    }

    /// <summary>
    /// Whether the rule will be considered a rule that only does validation and does not manipulate state in any way.
    /// When <c>true</c> rule is allowed to skip validation when the <see cref="BlockValidationContext.SkipValidation"/> is set to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// State in this context is the manipulation of information in the consensus data store based on actions specified in <see cref="Block"/> and <see cref="Transaction"/>.
    /// This will allow to ability to run validation checks on blocks (during mining for example) without change the underline store.
    /// </remarks>
    public class ValidationRule : RuleAttribute
    {
        /// <summary>A flag that specifies the rule can be skipped when the <see cref="RuleContext.SkipValidation"/> is set.</summary>
        public bool CanSkipValidation { get; set; }
    }

    /// <summary>
    /// Whether the rule is manipulating the consensus state, making changes to the store.
    /// </summary>
    public class ExecutionRule : RuleAttribute
    {
    }

    /// <summary>
    /// Rules that provide easy access to the <see cref="PosConsensusRules"/> parent.
    /// </summary>
    public abstract class PosConsensusRule : ConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRules PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRules;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }
    }
}