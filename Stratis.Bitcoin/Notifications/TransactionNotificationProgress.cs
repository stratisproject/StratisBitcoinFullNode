using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Notifications
{
    /// <summary>
    /// Class containing elements showing the progress of transactions notifications.
    /// </summary>
    public class TransactionNotificationProgress
    {
        public TransactionNotificationProgress()
        {
            this.TransactionsReceived = new Dictionary<uint256, uint256>();
        }

        /// <summary>
        /// Contains hashes of the transactions that have already been received from other nodes.
        /// </summary>
        public Dictionary<uint256, uint256> TransactionsReceived { get; set; }        
    }
}
