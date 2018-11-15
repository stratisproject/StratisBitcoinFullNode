using System;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Base rule to be used by all proven header validation rules.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Consensus.Rules.HeaderValidationConsensusRule" />
    public abstract class ProvenHeaderRuleBase : HeaderValidationConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;
        protected int ProvenHeadersActivationCheckpointHeight;
        protected CheckpointInfo ProvenHeadersActivationCheckpoint;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));

            var checkpoints = new Checkpoints(this.PosParent.Network, this.PosParent.ConsensusSettings);
            this.ProvenHeadersActivationCheckpoint = checkpoints.GetLastCheckpoint(out this.ProvenHeadersActivationCheckpointHeight);
        }

        /// <summary>
        /// Determines whether proven headers are activated based on the proven header activation height and applicable network.
        /// </summary>
        /// <param name="height">The height of the header.</param>
        /// <returns>
        /// <c>true</c> if proven header height is past the activation height for the corresponding network;
        /// otherwise, <c>false</c>.
        /// </returns>
        public bool IsProvenHeaderActivated(int height)
        {
            return height >= this.ProvenHeadersActivationCheckpointHeight;
        }

        /// <summary>
        /// Determines whether header validation should be skipped. This is true when header was
        /// presented by a whitelisted node and is not of type <see cref="ProvenBlockHeader" />.
        /// </summary>
        /// <param name="header">The block header.</param>
        /// <param name="networkPeer">The network peer.</param>
        /// <returns>
        ///   <c>true</c> if header is a <see cref="ProvenBlockHeader" />.
        /// </returns>
        public bool SkipValidation(BlockHeader header, INetworkPeer networkPeer)
        {
            return !(header is ProvenBlockHeader) && (networkPeer?.IsWhitelisted() == true);
        }

        /// <inheritdoc/>
        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;

            if (context.SkipValidation || !this.IsProvenHeaderActivated(chainedHeader.Height) || this.SkipValidation(chainedHeader.Header, context.ValidationContext.NetworkPeer))
                return;

            this.ProcessRule((PosRuleContext)context, chainedHeader, (ProvenBlockHeader)chainedHeader.Header);
        }

        /// <summary>
        /// Processes the rule.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="chainedHeader">The chained header to be validated.</param>
        /// <param name="header">The Proven Header to be validated.</param>
        protected abstract void ProcessRule(PosRuleContext context, ChainedHeader chainedHeader, ProvenBlockHeader header);
    }
}
