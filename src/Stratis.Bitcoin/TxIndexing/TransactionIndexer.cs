using System;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.TxIndexing
{
    public class TransactionIndexer : IDisposable
    {
        public TransactionIndexer(NodeSettings nodeSettings, ISignals signals)
        {
            // TODO class:
            // throw if indexing is disabled but any method is called
            // once indexing is enabled we require first block to be on top of the tip.
            // save the tip of the component
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

        /// <summary>Returns the total amount spent from the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        public Money GetSpentByAddress(BitcoinAddress address, int minConfirmations = 0)
        {
            throw new NotImplementedException();
        }


        // KV where key is address and value is a list of operations.
        //operation: type (spend\deposit), block height, amount

        public void Dispose()
        {
            // TODO
        }
    }
}
