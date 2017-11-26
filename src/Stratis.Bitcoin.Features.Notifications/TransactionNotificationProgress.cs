using System.Collections.Concurrent;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Notifications
{
    /// <summary>
    /// Class containing elements showing the progress of transactions notifications.
    /// </summary>
    public class TransactionNotificationProgress
    {
        public TransactionNotificationProgress()
        {
            this.TransactionsReceived = new ConcurrentDictionary<uint256, uint256>();
        }

        /// <summary>
        /// Contains hashes of the transactions that have already been received from other nodes.
        /// </summary>
        public ConcurrentDictionary<uint256, uint256> TransactionsReceived { get; set; }
    }
}
