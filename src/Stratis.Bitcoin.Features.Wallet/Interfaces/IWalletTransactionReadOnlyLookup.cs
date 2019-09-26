using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IWalletTransactionReadOnlyLookup
    {
        /// <summary>
        /// Determines if the outpoint has been added to this collection.
        /// </summary>
        /// <param name="outPoint">The transaction id.</param>
        /// <param name="addresses">Identifies the addresses related to the outpoint (if any).</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(OutPoint outPoint, out HashSet<AddressIdentifier> addresses);
    }
}
