using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class CoinviewHelper
    {
        /// <summary>
        /// Gets transactions identifiers that need to be fetched from store for specified block.
        /// </summary>
        /// <param name="block">The block with the transactions.</param>
        /// <param name="enforceBIP30">Whether to enforce look up of the transaction id itself and not only the reference to previous transaction id.</param>
        /// <returns>A list of transaction ids to fetch from store</returns>
        public uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
        {
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
                        ids.Add(input.PrevOut.Hash);
                }
            }

            uint256[] res = ids.ToArray();
            return res;
        }
    }
}
