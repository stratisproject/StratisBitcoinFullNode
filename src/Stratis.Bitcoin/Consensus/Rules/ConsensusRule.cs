using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Rules
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
        /// Execute the logic in the current rule in an async approach.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public virtual Task RunAsync(RuleContext context)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public virtual void Run(RuleContext context)
        {
            throw new NotImplementedException();
        }
    }

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

    /// <summary>
    /// An attribute that can be attached to a <see cref="ConsensusRule"/>.
    /// </summary>
    /// When <see cref="CanSkipValidation"/> is <c>true</c> rule is allowed to skip validation when the <see cref="RuleContext.SkipValidation"/> is set to <c>true</c>.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class RuleAttribute : Attribute
    {
        /// <summary>A flag that specifies the rule can be skipped when the <see cref="RuleContext.SkipValidation"/> is set.</summary>
        public bool CanSkipValidation { get; set; }
    }

    /// <summary>
    /// A rule that is used when a partial validation is performed.
    /// </summary>
    public class PartialValidationRuleAttribute : RuleAttribute
    {
    }

    /// <summary>
    /// A rule that is used when a full validation is performed.
    /// </summary>
    /// <remarks>
    /// Full validation in this context is the manipulation of information in the consensus data store based on actions specified in <see cref="Block"/> and <see cref="Transaction"/>.
    /// </remarks>
    public class FullValidationRuleAttribute : RuleAttribute
    {
    }

    /// <summary>
    /// A rule that is used when a header validation is performed.
    /// </summary>
    public class HeaderValidationRuleAttribute : RuleAttribute
    {
    }

    /// <summary>
    /// A rule that is used when integrity validation is performed on a received block.
    /// </summary>
    public class IntegrityValidationRuleAttribute : RuleAttribute
    {
    }

    /// <summary>
    /// A rule that is used when a transaction is received not as part of a block.
    /// </summary>
    public class MempoolRuleAttribute : RuleAttribute
    {
    }
}