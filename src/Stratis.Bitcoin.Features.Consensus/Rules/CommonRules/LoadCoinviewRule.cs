using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

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

            // Persist the changes to the coinview. This will likely only be stored in memory,
            // unless the coinview treashold is reached.
            this.Logger.LogTrace("Saving coinview changes.");
            var utxoRuleContext = context as UtxoRuleContext;
            await this.PowParent.UtxoSet.SaveChangesAsync(utxoRuleContext.UnspentOutputSet.GetCoins(this.PowParent.UtxoSet), null, oldBlockHash, nextBlockHash).ConfigureAwait(false);

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

            uint256[] ids = this.GetIdsToFetch(context.ValidationContext.BlockToValidate, context.Flags.EnforceBIP30);
            FetchCoinsResponse coins = await this.PowParent.UtxoSet.FetchCoinsAsync(ids).ConfigureAwait(false);
            utxoRuleContext.UnspentOutputSet.SetCoins(coins.UnspentOutputs);
        }

        /// <summary>
        /// The transactions identifiers that need to be fetched from store.
        /// </summary>
        /// <param name="block">The block with the transactions.</param>
        /// <param name="enforceBIP30">Whether to enforce look up of the transaction id itself and not only the reference to previous transaction id.</param>
        /// <returns>A list of transaction ids to fetch from store</returns>
        private uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
        {
            this.Logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(block), block.GetHash(), nameof(enforceBIP30), enforceBIP30);

            var ids = new HashSet<uint256>();
            foreach (Transaction tx in block.Transactions)
            {
                if (enforceBIP30)
                {
                    uint256 txId = tx.GetHash();
                    ids.Add(txId);
                }

                if (!tx.IsCoinBase)
                {
                    foreach (TxIn input in tx.Inputs)
                    {
                        ids.Add(input.PrevOut.Hash);
                    }
                }
            }

            uint256[] res = ids.ToArray();
            this.Logger.LogTrace("(-):*.{0}={1}", nameof(res.Length), res.Length);
            return res;
        }
    }
}