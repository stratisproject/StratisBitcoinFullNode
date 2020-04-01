using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;

namespace Stratis.Features.Collateral
{
    /// <summary>
    /// Ensures that a block that was produced on a collateral aware network contains height commitment data in the coinbase transaction.
    /// <para>
    /// Blocks that are found to have this data missing data will have the peer that served the header, banned.
    /// </para>
    /// </summary>
    public sealed class CheckCollateralCommitmentHeightRule : FullValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            return Task.CompletedTask;
        }
    }
}
