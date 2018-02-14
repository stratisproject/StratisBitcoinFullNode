using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// An abstract rule for implementing consensus rules.
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
        /// <summary>
        /// A special validation attribute that will be used by the engine to determine if validation
        /// whether this rules is a validation rule.
        /// </summary>
        private readonly ValidationRuleAttribute validationRuleAttribute;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public ConsensusRuleDescriptor(ConsensusRule rule)
        {
            this.Rule = rule;
            this.Attributes = Attribute.GetCustomAttributes(rule.GetType()).OfType<RuleAttribute>().ToList();

            this.validationRuleAttribute = this.Attributes.OfType<ValidationRuleAttribute>().FirstOrDefault();
        }

        /// <summary>Rules that are strictly validation can be skipped unless the <see cref="ValidationRuleAttribute.CanSkipValidation"/> is <c>false</c>.</summary>
        public bool CanSkipValidation => this.validationRuleAttribute?.CanSkipValidation ?? true;

        /// <summary>The rule represented by this descriptor.</summary>
        public ConsensusRule Rule { get; }

        /// <summary>The collection of <see cref="RuleAttribute"/> that are attached to this rule.</summary>
        public List<RuleAttribute> Attributes { get; }
    }

    /// <summary>
    /// An attribute that can be attached to a <see cref="ConsensusRule"/>.
    /// </summary>
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
    public class ValidationRuleAttribute : RuleAttribute
    {
        /// <summary>A flag that specifies the rule can be skipped when the <see cref="RuleContext.SkipValidation"/> is set.</summary>
        public bool CanSkipValidation { get; set; }
    }

    /// <summary>
    /// Whether the rule is manipulating the consensus state, making changes to the store.
    /// </summary>
    public class ExecutionRuleAttribute : RuleAttribute
    {
    }

    /// <summary>
    /// Whether the rule is manipulating the consensus state, making changes to the store.
    /// </summary>
    public class MempoolRuleAttribute : RuleAttribute
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