using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class UnspentOutputSet
    {
        private Dictionary<uint256, UnspentOutputs> unspents;

        public TxOut GetOutputFor(TxIn txIn)
        {
            UnspentOutputs unspent = this.unspents.TryGet(txIn.PrevOut.Hash);
            if (unspent == null)
                return null;

            return unspent.TryGetOutput(txIn.PrevOut.N);
        }

        public bool HaveInputs(Transaction tx)
        {
            return tx.Inputs.All(txin => this.GetOutputFor(txin) != null);
        }

        public UnspentOutputs AccessCoins(uint256 uint256)
        {
            return this.unspents.TryGet(uint256);
        }

        public Money GetValueIn(Transaction tx)
        {
            return tx.Inputs.Select(txin => this.GetOutputFor(txin).Value).Sum();
        }

        /// <summary>
        /// Adds transaction's outputs to unspent coins list and removes transaction's inputs from it.
        /// </summary>
        /// <param name="transaction">Transaction which inputs and outputs are used for updating unspent coins list.</param>
        /// <param name="height">Height of a block that contains target transaction.</param>
        public void Update(Transaction transaction, int height)
        {
            if (!transaction.IsCoinBase)
            {
                foreach (TxIn input in transaction.Inputs)
                {
                    UnspentOutputs c = this.AccessCoins(input.PrevOut.Hash);

                    c.Spend(input.PrevOut.N);
                }
            }

            this.unspents.AddOrReplace(transaction.GetHash(), new UnspentOutputs((uint)height, transaction));
        }

        public void SetCoins(UnspentOutputs[] coins)
        {
            this.unspents = new Dictionary<uint256, UnspentOutputs>(coins.Length);
            foreach (UnspentOutputs coin in coins)
            {
                if (coin != null)
                {
                    this.unspents.Add(coin.TransactionId, coin);
                }
            }
        }

        public void TrySetCoins(UnspentOutputs[] coins)
        {
            this.unspents = new Dictionary<uint256, UnspentOutputs>(coins.Length);
            foreach (UnspentOutputs coin in coins)
            {
                if (coin != null)
                    this.unspents.TryAdd(coin.TransactionId, coin);
            }
        }

        public IList<UnspentOutputs> GetCoins()
        {
            return this.unspents.Select(u => u.Value).ToList();
        }
    }
}
