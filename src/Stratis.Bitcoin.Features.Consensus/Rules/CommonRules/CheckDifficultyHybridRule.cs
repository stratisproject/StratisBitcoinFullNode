using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Calculate the difficulty of a POS network for both Pow/POS blocks.</summary>
    public class CheckDifficultyHybridRule : FullValidationConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.HighHash">Thrown if block doesn't have a valid PoW header.</exception>
        /// <exception cref="ConsensusErrors.BadDiffBits">Thrown if proof of stake is incorrect.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            var posRuleContext = context as PosRuleContext;

            posRuleContext.BlockStake = BlockStake.Load(context.ValidationContext.BlockToValidate);

            if (posRuleContext.BlockStake.IsProofOfWork())
            {
                if (!context.ValidationContext.BlockToValidate.Header.CheckProofOfWork())
                {
                    this.Logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }
            }

            Target nextWorkRequired = this.PosParent.StakeValidator.GetNextTargetRequired(this.PosParent.StakeChain,
                context.ValidationContext.ChainedHeaderToValidate.Previous, this.Parent.Network.Consensus, posRuleContext.BlockStake.IsProofOfStake());

            BlockHeader header = context.ValidationContext.BlockToValidate.Header;

            // Check proof of stake.
            if (header.Bits != nextWorkRequired)
            {
                this.Logger.LogTrace("(-)[BAD_DIFF_BITS]");
                ConsensusErrors.BadDiffBits.Throw();
            }

            return Task.CompletedTask;
        }
    }
}