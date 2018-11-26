using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class SaveCoinviewRule : UtxoStoreConsensusRule
    {
        /// <summary>
        /// Specifies time threshold which is used to determine if flush is required.
        /// When consensus tip timestamp is greater than current time minus the threshold the flush is required.
        /// </summary>
        /// <remarks>Used only on blockchains without max reorg property.</remarks>
        private const int FlushRequiredThresholdSeconds = 2 * 24 * 60 * 60;

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            uint256 oldBlockHash = context.ValidationContext.ChainedHeaderToValidate.Previous.HashBlock;
            uint256 nextBlockHash = context.ValidationContext.ChainedHeaderToValidate.HashBlock;
            int height = context.ValidationContext.ChainedHeaderToValidate.Height;

            // Persist the changes to the coinview. This will likely only be stored in memory,
            // unless the coinview treashold is reached.
            this.Logger.LogTrace("Saving coinview changes.");
            var utxoRuleContext = context as UtxoRuleContext;
            await this.PowParent.UtxoSet.SaveChangesAsync(utxoRuleContext.UnspentOutputSet.GetCoins(), null, oldBlockHash, nextBlockHash, height).ConfigureAwait(false);

            // Use the default flush condition to decide if flush is required (currently set to every 60 seconds)
            if (this.PowParent.UtxoSet is CachedCoinView cachedCoinView)
                await cachedCoinView.FlushAsync(false).ConfigureAwait(false);
        }
    }

    public class LoadCoinviewRule : UtxoStoreConsensusRule
    {
        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            // Check that the current block has not been reorged.
            // Catching a reorg at this point will not require a rewind.
            if (context.ValidationContext.BlockToValidate.Header.HashPrevBlock != this.Parent.ChainState.ConsensusTip.HashBlock)
            {
                this.Logger.LogTrace("Reorganization detected.");
                ConsensusErrors.InvalidPrevTip.Throw();
            }

            var utxoRuleContext = context as UtxoRuleContext;

            // Load the UTXO set of the current block. UTXO may be loaded from cache or from disk.
            // The UTXO set is stored in the context.
            this.Logger.LogTrace("Loading UTXO set of the new block.");
            utxoRuleContext.UnspentOutputSet = new UnspentOutputSet();

            uint256[] ids = this.coinviewHelper.GetIdsToFetch(context.ValidationContext.BlockToValidate, context.Flags.EnforceBIP30);
            FetchCoinsResponse coins = await this.PowParent.UtxoSet.FetchCoinsAsync(ids).ConfigureAwait(false);
            utxoRuleContext.UnspentOutputSet.SetCoins(coins.UnspentOutputs);
        }
    }
}