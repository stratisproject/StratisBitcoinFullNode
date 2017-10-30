using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public enum MatchLocation
    {
        Output,
        Input,
    }
    public class MatchedRule
    {
        public uint Index
        {
            get;
            set;
        }

        public WalletRule Rule
        {
            get;
            set;
        }

        public MatchLocation MatchType
        {
            get;
            set;
        }

        public override string ToString()
        {
            return Index + "-" + MatchType.ToString();
        }
    }
}
