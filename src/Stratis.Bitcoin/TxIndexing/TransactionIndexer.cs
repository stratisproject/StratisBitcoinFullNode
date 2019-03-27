using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.TxIndexing
{
    public class TransactionIndexer
    {
        public TransactionIndexer(NodeSettings nodeSettings)
        {

        }

        /// <summary>Returns balance of the given address confirmed with at least <paramref name="minConfirmations"/> confirmations.</summary>
        public Money GetAddressBalance(BitcoinAddress address, int minConfirmations = 0)
        {
            throw new NotImplementedException();
        }

        /// <summary>Returns the total amount received by the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        public Money GetReceivedByAddress(BitcoinAddress address, int minConfirmations = 0)
        {
            throw new NotImplementedException();
        }
    }
}
