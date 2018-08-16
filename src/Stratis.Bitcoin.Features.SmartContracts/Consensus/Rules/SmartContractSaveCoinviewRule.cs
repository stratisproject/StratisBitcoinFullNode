using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    public sealed class SmartContractSaveCoinviewRule : UtxoStoreConsensusRule
    {
        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            ChainedHeader currentBlock = context.ValidationContext.ChainTipToExtend;

            // Persist the changes to the coinview. This will likely only be stored in memory,
            // unless the coinview threshold is reached.
            this.Logger.LogTrace("Saving coinview changes.");
            var utxoRuleContext = (UtxoRuleContext)context;
            await this.PowParent.UtxoSet.AddRewindDataAsync(utxoRuleContext.UnspentOutputSet.GetCoins(this.PowParent.UtxoSet), currentBlock).ConfigureAwait(false);
        }
    }
}
