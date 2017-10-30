using NBitcoin.Crypto;
using NBitcoin.Indexer.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class WalletRuleEntryCollection : IEnumerable<WalletRuleEntry>
    {
        readonly List<WalletRuleEntry> _WalletRules;
        readonly HashSet<Tuple<string,string>> _WalletsIds = new HashSet<Tuple<string,string>>();

        readonly MultiValueDictionary<string, WalletRuleEntry> _EntriesByWallet;
        readonly ILookup<string, WalletRuleEntry> _EntriesByWalletLookup;

        readonly MultiValueDictionary<Script, WalletRuleEntry> _EntriesByAddress;
        readonly ILookup<Script, WalletRuleEntry> _EntriesByAddressLookup;


        internal WalletRuleEntryCollection(IEnumerable<WalletRuleEntry> walletRules)
        {
            if(walletRules == null)
                throw new ArgumentNullException("walletRules");

            _WalletRules = new List<WalletRuleEntry>();            
            _EntriesByWallet = new MultiValueDictionary<string, WalletRuleEntry>();
            _EntriesByWalletLookup = _EntriesByWallet.AsLookup();

            _EntriesByAddress = new MultiValueDictionary<Script, WalletRuleEntry>();
            _EntriesByAddressLookup = _EntriesByAddress.AsLookup();
            foreach(var rule in walletRules)
            {
                Add(rule);
            }
        }

        public int Count
        {
            get
            {
                return _WalletRules.Count;
            }
        }

        public bool Add(WalletRuleEntry entry)
        {
            if(!_WalletsIds.Add(GetId(entry)))
                return false;
            _WalletRules.Add(entry);
            _EntriesByWallet.Add(entry.WalletId, entry);
            var rule = entry.Rule as ScriptRule;
            if(rule != null)
                _EntriesByAddress.Add(rule.ScriptPubKey, entry);
            return true;
        }

        private Tuple<string,string> GetId(WalletRuleEntry entry)
        {
            return Tuple.Create(entry.WalletId, entry.Rule.Id);
        }
        public void AddRange(IEnumerable<WalletRuleEntry> entries)
        {
            foreach(var entry in entries)
                Add(entry);
        }

        public IEnumerable<WalletRuleEntry> GetRulesForWallet(string walletName)
        {
            return _EntriesByWalletLookup[walletName];
        }


        public IEnumerable<WalletRuleEntry> GetRulesFor(IDestination destination)
        {
            return GetRulesFor(destination.ScriptPubKey);
        }

        public IEnumerable<WalletRuleEntry> GetRulesFor(Script script)
        {
            return _EntriesByAddressLookup[script];
        }

        #region IEnumerable<WalletRuleEntry> Members

        public IEnumerator<WalletRuleEntry> GetEnumerator()
        {
            return _WalletRules.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
