using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public static class IWalletRepositoryExt
    {
        public static readonly int TransactionBatchSize = 50000;

        public static List<HashSet<string>> GetAddressGroupings(this IWalletRepository repo, Network network, string walletName)
        {
            var addressGroupings = new Dictionary<string, HashSet<string>>();

            TransactionData prev = null;

            while (true)
            {
                // Read the transactions in batches.
                List<TransactionData> transactions = repo.GetAllTransactions(walletName, null, null, null, TransactionBatchSize, prev, false).ToList();
                if (transactions.Count == 0)
                    break;

                prev = transactions[transactions.Count - 1];

                foreach (var transaction in transactions)
                {
                    var addressBase58 = transaction.ScriptPubKey.GetDestinationAddress(network);
                    if (addressBase58 == null)
                        continue;

                    // Group all input addresses with each other.
                    if (transaction.IsSpent())
                    {
                        var spendTxId = transaction.SpendingDetails.TransactionId.ToString();
                        if (!addressGroupings.TryGetValue(spendTxId, out HashSet<string> grouping))
                        {
                            grouping = new HashSet<string>();
                            addressGroupings[spendTxId] = grouping;
                        }

                        grouping.Add(addressBase58.ToString());
                    }

                    // Include any change addresses.
                    {
                        if (addressGroupings.TryGetValue(transaction.Id.ToString(), out HashSet<string> grouping))
                            grouping.Add(addressBase58.ToString());
                    }
                }
            }

            var uniqueGroupings = new List<HashSet<string>>();
            var setMap = new Dictionary<string, HashSet<string>>();

            foreach ((string spendTxId, HashSet<string> grouping) in addressGroupings.Select(kv => (kv.Key, kv.Value)))
            {
                // Create a list of unique groupings intersecting this grouping.
                var hits = new List<HashSet<string>>();
                foreach (string addressBase58 in grouping)
                    if (setMap.TryGetValue(addressBase58, out HashSet<string> it))
                        hits.Add(it);

                // Merge the matching uinique groupings into this grouping and remove the old groupings.
                foreach (HashSet<string> hit in hits)
                {
                    grouping.UnionWith(hit);
                    uniqueGroupings.Remove(hit);
                }

                // Add the new merged grouping.
                uniqueGroupings.Add(grouping);

                // Update the set map which maps addresses to the unique grouping they appear in.
                foreach (string addressBase58 in grouping)
                    setMap[addressBase58] = grouping;
            }

            return uniqueGroupings;
        }
    }
}
