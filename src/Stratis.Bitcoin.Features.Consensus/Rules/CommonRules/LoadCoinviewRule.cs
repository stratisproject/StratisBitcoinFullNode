using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    [ExecutionRule]
    public class LoadCoinviewRule : UtxoStoreConsensusRule
    {
        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            // Load the UTXO set of the current block. UTXO may be loaded from cache or from disk.
            // The UTXO set is stored in the context.
            this.Logger.LogTrace("Loading UTXO set of the new block.");
            context.Set = new UnspentOutputSet();
            using (new StopwatchDisposable(o => this.Parent.PerformanceCounter.AddUTXOFetchingTime(o)))
            {
                uint256[] ids = this.GetIdsToFetch(context.BlockValidationContext.Block, context.Flags.EnforceBIP30);
                FetchCoinsResponse coins = await this.PowParent.UtxoSet.FetchCoinsAsync(ids).ConfigureAwait(false);
                context.Set.SetCoins(coins.UnspentOutputs);
            }

            // Attempt to load into the cache the next set of UTXO to be validated.
            // The task is not awaited so will not stall main validation process.
            this.TryPrefetchAsync(context.Flags);
        }

        /// <summary>
        /// This method tries to load from cache the UTXO of the next block in a background task.
        /// </summary>
        /// <param name="flags">Information about activated features.</param>
        private async void TryPrefetchAsync(DeploymentFlags flags)
        {
            this.Logger.LogTrace("({0}:{1})", nameof(flags), flags);

            if (this.PowParent.UtxoSet is CachedCoinView)
            {
                Block nextBlock = this.PowParent.Puller.TryGetLookahead(0);
                if (nextBlock != null)
                    await this.PowParent.UtxoSet.FetchCoinsAsync(this.GetIdsToFetch(nextBlock, flags.EnforceBIP30)).ConfigureAwait(false);
            }

            this.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// The transactions identifiers that need to be fetched from store. 
        /// </summary>
        /// <param name="block">The block with the transactions.</param>
        /// <param name="enforceBIP30">Whether to enforce look up of the transaction id itself and not only the reference to previous transaction id.</param>
        /// <returns>A list of transaction ids to fetch from store</returns>
        private uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
        {
            this.Logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(block), block.GetHash(NetworkOptions.TemporaryOptions), nameof(enforceBIP30), enforceBIP30);

            HashSet<uint256> ids = new HashSet<uint256>();
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