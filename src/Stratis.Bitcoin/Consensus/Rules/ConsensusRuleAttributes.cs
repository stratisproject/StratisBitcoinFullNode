using System;

namespace Stratis.Bitcoin.Consensus.Rules
{
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