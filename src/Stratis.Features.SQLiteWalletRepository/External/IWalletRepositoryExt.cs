using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public static class IWalletRepositoryExt
    {
        public static List<HashSet<string>> GetAddressGroupings(this IWalletRepository repo, Network network, string walletName)
        {
            var txs = repo.GetWalletTransactionLookup(walletName);
            var addresses = repo.GetWalletAddressLookup(walletName);
            var addressGroupings = new List<HashSet<string>>();
            var loneGrouping = new HashSet<string>();

            TransactionData prev = null;

            while (true)
            {
                // Only load 100 transactions at a time into memory.
                List<TransactionData> transactions = repo.GetAllTransactions(walletName, null, null, null, 100, prev).ToList();
                if (transactions.Count == 0)
                    break;

                prev = transactions[transactions.Count - 1];

                foreach (var transaction in transactions)
                {
                    // Identify which of my transactions feed into this as spend transaction.
                    var addressGroupBase58 = new HashSet<string>();

                    // Group all input addresses with each other.
                    foreach (var txIn in repo.GetTransactionInputs(walletName, transaction.CreationTime, transaction.Id))
                    {
                        var outPoint = new OutPoint(txIn.Id, txIn.Index);

                        if (txs.Contains(outPoint, out HashSet<AddressIdentifier> addressList))
                        {
                            foreach (AddressIdentifier address in addressList)
                            {
                                var scriptPubKey = Script.FromHex(address.ScriptPubKey);

                                // Get the txIn's previous transaction address.
                                var addressBase58 = scriptPubKey.GetDestinationAddress(network);
                                if (addressBase58 == null)
                                    continue;

                                addressGroupBase58.Add(addressBase58.ToString());
                            }
                        }
                    }

                    // If any of the inputs were "mine", also include any change addresses associated to the transaction.
                    if (addressGroupBase58.Any())
                    {
                        foreach (var txOut in repo.GetTransactionOutputs(walletName, transaction.CreationTime, transaction.Id))
                        {
                            if (addresses.Contains(txOut.ScriptPubKey, out AddressIdentifier address) && address.AddressType == 1)
                            {
                                var txOutAddressBase58 = txOut.ScriptPubKey.GetDestinationAddress(network);
                                if (txOutAddressBase58 == null)
                                    continue;

                                addressGroupBase58.Add(txOutAddressBase58.ToString());
                            }
                        }

                        addressGroupings.Add(addressGroupBase58);
                    }

                    // Group lone addresses by themselves.
                    foreach (var txOut in repo.GetTransactionOutputs(walletName, transaction.CreationTime, transaction.Id))
                    {
                        if (addresses.Contains(txOut.ScriptPubKey, out AddressIdentifier address))
                        {
                            var addressBase58 = txOut.ScriptPubKey.GetDestinationAddress(network);
                            if (addressBase58 == null)
                                continue;

                            loneGrouping.Add(addressBase58.ToString());
                        }
                    }
                }
            }

            foreach (string addressBase58 in loneGrouping)
            {
                var grouping = new HashSet<string>();
                grouping.Add(addressBase58.ToString());
                addressGroupings.Add(grouping);
            }

            // Merge the results into a distinct set of grouped addresses.
            var uniqueGroupings = new List<HashSet<string>>();
            foreach (var addressGroup in addressGroupings)
            {
                var addressGroupDistinct = addressGroup.Distinct();

                HashSet<string> existing = null;

                foreach (var address in addressGroupDistinct)
                {
                    // If the address was found to be apart of an existing group add it here.
                    // The assumption here is that if we have a grouping of [a,b], finding [a] would have returned
                    // the existing set and we can just add the address to that set.
                    if (existing != null)
                    {
                        var existingAddress = existing.FirstOrDefault(a => a == address);
                        if (existingAddress == null)
                            existing.Add(address);

                        continue;
                    }

                    // Check if the address already exists in a group.
                    // If it does not, add the distinct set into the unique groupings list,
                    // thereby creating a new "grouping".
                    existing = uniqueGroupings.FirstOrDefault(g => g.Contains(address));
                    if (existing == null)
                        uniqueGroupings.Add(new HashSet<string>(addressGroupDistinct));
                }
            }

            return uniqueGroupings.ToList();
        }
    }
}
