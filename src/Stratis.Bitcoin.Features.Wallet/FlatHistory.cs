using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class AccountHistory
    {
        /// <summary>
        /// The account for which the history is retrieved.
        /// </summary>
        public HdAccount Account { get; set; }

        /// <summary>
        /// The collection of history items.
        /// </summary>
        public IEnumerable<FlatHistory> History { get; set; }
    }

    /// <summary>
    /// A class that represents a flat view of the wallets history.
    /// </summary>
    public class FlatHistory
    {
        /// <summary>
        /// The address associated with this UTXO.
        /// </summary>
        public HdAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }
    }
}