using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class ReceiptMatcher
    {
        /// <summary>
        /// Matches the given receipts against the supplied address and topics, and returns those that are present in the filter.
        /// </summary>
        /// <param name="receipts"></param>
        /// <param name="address"></param>
        /// <param name="topics"></param>
        /// <returns></returns>
        public List<Receipt> MatchReceipts(IEnumerable<Receipt> receipts, uint160 address, IEnumerable<byte[]> topics)
        {
            // For each block, get all receipts, and if they match, add to list to return.
            var receiptResponses = new List<Receipt>();

            foreach (Receipt storedReceipt in receipts)
            {
                if (storedReceipt == null) // not a smart contract transaction. Move to next transaction.
                    continue;

                // Match the receipts where all data passes the filter.
                if (storedReceipt.Logs.Any(log => BloomExtensions.Test(log.GetBloom(), address, topics)))
                    receiptResponses.Add(storedReceipt);
            }

            return receiptResponses;
        }
    }
}