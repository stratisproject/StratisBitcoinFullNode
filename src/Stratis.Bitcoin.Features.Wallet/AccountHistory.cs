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
}