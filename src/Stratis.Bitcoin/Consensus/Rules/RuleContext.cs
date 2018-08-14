using System;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Rules
{
    /// <summary>
    /// Context that contains variety of information regarding blocks validation and execution.
    /// </summary>
    public class RuleContext
    {
        public NBitcoin.Consensus Consensus { get; set; }

        public DateTimeOffset Time { get; set; }

        public ValidationContext ValidationContext { get; set; }

        public DeploymentFlags Flags { get; set; }

        /// <summary>Indicate the block was created by our node.</summary>
        public bool MinedBlock { get; set; }

        /// <summary>Whether to skip block validation for this block due to either a checkpoint or assumevalid hash set.</summary>
        public bool SkipValidation { get; set; }

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader ConsensusTip { get; set; }

        /// <summary>The height of the consensus tip.</summary>
        public int ConsensusTipHeight { get; set; }

        public RuleContext()
        {
        }

        public RuleContext(ValidationContext validationContext, NBitcoin.Consensus consensus, ChainedHeader consensusTip, DateTimeOffset time) : base()
        {
            Guard.NotNull(validationContext, nameof(validationContext));
            Guard.NotNull(consensus, nameof(consensus));

            this.ValidationContext = validationContext;
            this.Consensus = consensus;
            this.ConsensusTip = consensusTip;
            this.ConsensusTipHeight = consensusTip.Height;
            this.Time = time;
            this.MinedBlock = false;
        }
    }
}