using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Check that a <see cref="BitcoinMain"/> network block has the correct version according to the defined active deployments.
    /// </summary>
    [HeaderValidationRule]
    public class BitcoinActivationRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.ConsensusTip, nameof(context.ConsensusTip));

            BlockHeader header = context.ValidationContext.ChainedHeader.Header;

            int height = context.ConsensusTipHeight + 1;

            // Reject outdated version blocks when 95% (75% on testnet) of the network has upgraded:
            // check for version 2, 3 and 4 upgrades.
            // TODO: this checks need to be moved to their respective validation rules.
            if (((header.Version < 2) && (height >= this.Parent.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34])) ||
                ((header.Version < 3) && (height >= this.Parent.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66])) ||
                ((header.Version < 4) && (height >= this.Parent.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65])))
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            return Task.CompletedTask;
        }
    }
}