using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class BalanceSheet
    {
        private readonly ChainBase _Chain;
        public ChainBase Chain
        {
            get
            {
                return _Chain;
            }
        }
        public BalanceSheet(IEnumerable<OrderedBalanceChange> changes, ChainBase chain)
        {
            if (chain == null)
                throw new ArgumentNullException("chain");
            _Chain = chain;

            var all = changes
                        .Where(c => c.SpentCoins != null) //Remove line whose previous coins have not been loadedcould not be deduced
                        .Where(c => c.MempoolEntry || chain.GetBlock(c.BlockId) != null) //Take only mempool entry, or confirmed one
                        .Where(c => !(c.IsCoinbase && c.MempoolEntry)) //There is no such thing as a Coinbase unconfirmed, by definition a coinbase appear in a block
                        .ToList(); 
            var confirmed = all.Where(o => o.BlockId != null).ToDictionary(o => o.TransactionId);
            Dictionary<uint256, OrderedBalanceChange> unconfirmed = new Dictionary<uint256, OrderedBalanceChange>();

            foreach(var item in all.Where(o => o.MempoolEntry && !confirmed.ContainsKey(o.TransactionId)))
            {
                unconfirmed.AddOrReplace(item.TransactionId, item);
            }

            _Prunable = all.Where(o => o.BlockId == null && confirmed.ContainsKey(o.TransactionId)).ToList();
            _All = all.Where(o => 
                (unconfirmed.ContainsKey(o.TransactionId) || confirmed.ContainsKey(o.TransactionId)) 
                    &&
                    !(o.BlockId == null && confirmed.ContainsKey(o.TransactionId))
                ).ToList();
            _Confirmed = _All.Where(o => o.BlockId != null && confirmed.ContainsKey(o.TransactionId)).ToList();
            _Unconfirmed = _All.Where(o => o.BlockId == null && unconfirmed.ContainsKey(o.TransactionId)).ToList();
        }

        private readonly List<OrderedBalanceChange> _Unconfirmed;
        public List<OrderedBalanceChange> Unconfirmed
        {
            get
            {
                return _Unconfirmed;
            }
        }
        private readonly List<OrderedBalanceChange> _Confirmed;
        public List<OrderedBalanceChange> Confirmed
        {
            get
            {
                return _Confirmed;
            }
        }

        private readonly List<OrderedBalanceChange> _All;
        public List<OrderedBalanceChange> All
        {
            get
            {
                return _All;
            }
        }
        private readonly List<OrderedBalanceChange> _Prunable;
        public List<OrderedBalanceChange> Prunable
        {
            get
            {
                return _Prunable;
            }
        }

    }
}
